using NAudio.Wave;

namespace NeonHiFi.Audio.Visualization;

/// <summary>
/// Taps a source ISampleProvider, computing per-channel RMS each block and
/// feeding it through <see cref="VuBallistics"/>. Unlike the FFT spectrum
/// analyzer, an RMS-of-a-block plus a couple of float multiplies for the
/// ballistics is cheap enough to do directly in Read() - no background
/// thread needed. Results are published to a thread-safe snapshot the UI
/// layer can poll at its own rate (Phase 3's concern, not this one).
/// </summary>
public sealed class VuMeter : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly VuBallistics[] _channels;
    private float[] _latestNeedleLevels;
    private float[] _latestPeakLevels;

    public VuMeter(ISampleProvider source)
    {
        _source = source;

        _channels = new VuBallistics[source.WaveFormat.Channels];
        for (var i = 0; i < _channels.Length; i++)
        {
            _channels[i] = new VuBallistics();
        }

        _latestNeedleLevels = new float[_channels.Length];
        _latestPeakLevels = new float[_channels.Length];
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);

        var channels = WaveFormat.Channels;
        var frameCount = samplesRead / channels;
        if (frameCount == 0)
        {
            return samplesRead;
        }

        var deltaTimeSeconds = (float)frameCount / WaveFormat.SampleRate;

        var sumSquares = new double[channels];
        for (var i = 0; i < samplesRead; i++)
        {
            var channel = i % channels;
            var sample = buffer[offset + i];
            sumSquares[channel] += sample * sample;
        }

        var needleLevels = new float[channels];
        var peakLevels = new float[channels];
        for (var c = 0; c < channels; c++)
        {
            var rms = (float)Math.Sqrt(sumSquares[c] / frameCount);
            _channels[c].Update(rms, deltaTimeSeconds);
            needleLevels[c] = _channels[c].NeedleLevel;
            peakLevels[c] = _channels[c].PeakLevel;
        }

        Volatile.Write(ref _latestNeedleLevels, needleLevels);
        Volatile.Write(ref _latestPeakLevels, peakLevels);

        return samplesRead;
    }

    /// <summary>Thread-safe snapshot of each channel's smoothed needle level (0-1 linear).</summary>
    public float[] GetNeedleLevels() => Volatile.Read(ref _latestNeedleLevels);

    /// <summary>Thread-safe snapshot of each channel's peak-hold level (0-1 linear).</summary>
    public float[] GetPeakLevels() => Volatile.Read(ref _latestPeakLevels);
}
