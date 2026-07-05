using System.Collections.Concurrent;
using NAudio.Wave;

namespace NeonHiFi.Audio.Visualization;

/// <summary>
/// Taps a source ISampleProvider, passing audio through completely unchanged
/// while handing a copy of each block to a dedicated background thread that
/// mono-sums, windows, and FFTs it via <see cref="SpectrumProcessor"/>.
/// Read() only ever does a cheap array copy - the FFT itself never runs on
/// whatever thread is pulling audio (the real-time output thread), per
/// CLAUDE.md's real-time audio conventions.
/// </summary>
public sealed class SpectrumAnalyzer : ISampleProvider, IDisposable
{
    private const int MaxQueuedBlocks = 64;

    private readonly ISampleProvider _source;
    private readonly SpectrumProcessor _processor;
    private readonly ConcurrentQueue<float[]> _incomingBlocks = new();
    private readonly SemaphoreSlim _dataAvailable = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _analysisThread;
    private readonly float[] _accumulationBuffer;
    private int _accumulatedCount;
    private float[] _latestMagnitudesDb;

    public SpectrumAnalyzer(ISampleProvider source, int fftSize = 2048, int bandCount = 24)
    {
        _source = source;
        _processor = new SpectrumProcessor(source.WaveFormat.SampleRate, fftSize, bandCount);
        _accumulationBuffer = new float[fftSize];
        _latestMagnitudesDb = new float[bandCount];

        _analysisThread = new Thread(AnalysisLoop) { IsBackground = true, Name = "NeonHiFi Spectrum Analysis" };
        _analysisThread.Start();
    }

    /// <summary>Raised on the background analysis thread each time a new window finishes processing.</summary>
    public event EventHandler<float[]>? SpectrumUpdated;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);

        var channels = WaveFormat.Channels;
        var frameCount = samplesRead / channels;
        var monoBlock = new float[frameCount];
        for (int frame = 0, i = 0; frame < frameCount; frame++, i += channels)
        {
            float sum = 0;
            for (var c = 0; c < channels; c++)
            {
                sum += buffer[offset + i + c];
            }

            monoBlock[frame] = sum / channels;
        }

        _incomingBlocks.Enqueue(monoBlock);
        while (_incomingBlocks.Count > MaxQueuedBlocks)
        {
            _incomingBlocks.TryDequeue(out _);
        }

        _dataAvailable.Release();

        return samplesRead;
    }

    /// <summary>Thread-safe snapshot of the most recent analysis result.</summary>
    public float[] GetLatestMagnitudesDb() => Volatile.Read(ref _latestMagnitudesDb);

    public void Dispose()
    {
        _cts.Cancel();
        _dataAvailable.Release();
        _analysisThread.Join(TimeSpan.FromSeconds(1));
        _cts.Dispose();
        _dataAvailable.Dispose();
    }

    private void AnalysisLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (!_dataAvailable.Wait(200) || _cts.IsCancellationRequested)
            {
                continue;
            }

            while (_incomingBlocks.TryDequeue(out var block))
            {
                AccumulateAndProcess(block);
            }
        }
    }

    private void AccumulateAndProcess(float[] block)
    {
        var offset = 0;
        while (offset < block.Length)
        {
            var spaceLeft = _accumulationBuffer.Length - _accumulatedCount;
            var toCopy = Math.Min(spaceLeft, block.Length - offset);
            Array.Copy(block, offset, _accumulationBuffer, _accumulatedCount, toCopy);
            _accumulatedCount += toCopy;
            offset += toCopy;

            if (_accumulatedCount == _accumulationBuffer.Length)
            {
                var magnitudes = _processor.Process(_accumulationBuffer);
                Volatile.Write(ref _latestMagnitudesDb, magnitudes);
                SpectrumUpdated?.Invoke(this, magnitudes);
                _accumulatedCount = 0;
            }
        }
    }
}
