using NeonHiFi.Audio.Capture;
using NeonHiFi.Audio.Dsp;
using NeonHiFi.Audio.Output;

namespace NeonHiFi.Audio.Pipeline;

/// <summary>
/// Owns the full capture -> EQ -> output chain as a single unit, connected
/// via NAudio's ISampleProvider/BufferedWaveProvider (the "buffered provider"
/// alternative to a hand-rolled ring buffer this pipeline's issue called out).
///
/// Both underlying services can restart independently (capture on a
/// default-device change, output on playback failure) - this class keeps
/// them in sync, since capture handing out a *new* ISampleProvider after a
/// restart would otherwise leave a running output stuck reading a dead one.
/// A restart also means a *new* GraphicEqualizer wrapping the new source
/// (its coefficients depend on the capture format, which could change if the
/// new default device has a different sample rate) - <see cref="_bandGains"/>
/// remembers the user's gain settings across that so they aren't lost.
///
/// Measured end-to-end latency: ~470-520ms on this machine (avg ~495ms over
/// 3 runs). Methodology: a marker burst was played into the default render
/// device via a separate WasapiOut, and its arrival timed via a second,
/// independent loopback capture tapped directly on the pipeline's chosen
/// output device - so the figure covers the full real path (source render
/// -> this pipeline's capture -> its output re-render -> the measurement
/// tap), not just a theoretical buffer-size sum. It's higher than the
/// ~200-250ms a naive "just add the buffer sizes" estimate would suggest,
/// which is itself worth knowing: real overhead (thread scheduling, the
/// measurement's own marker/detector taps) meaningfully exceeds the
/// configured buffers. AudioOutputService's 200ms WasapiOut buffer is the
/// main lever - lowering AudioOutputService.LatencyMilliseconds would
/// reduce this at the cost of higher underrun risk.
/// </summary>
public sealed class AudioPipeline : IDisposable
{
    private readonly object _lock = new();
    private readonly AudioCaptureService _capture = new();
    private readonly AudioOutputService _output = new();
    private readonly float[] _bandGains = new float[GraphicEqualizer.StandardCenterFrequencies.Length];
    private string? _outputDeviceId;

    public bool IsRunning { get; private set; }

    public GraphicEqualizer? Equalizer { get; private set; }

    public AudioPipeline()
    {
        _capture.CaptureRestarted += (_, _) => ReconnectOutput();
        _output.PlaybackStopped += (_, _) => ReconnectOutput();
    }

    public void Start(string? outputDeviceId = null)
    {
        lock (_lock)
        {
            if (IsRunning)
            {
                return;
            }

            _outputDeviceId = outputDeviceId;
            _capture.Start();
            StartOutputFromCapture();
            IsRunning = true;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!IsRunning)
            {
                return;
            }

            // Stop output first so it's never left pulling from a capture
            // provider that's about to be torn down.
            _output.Stop();
            _capture.Stop();
            Equalizer = null;
            IsRunning = false;
        }
    }

    public void Dispose()
    {
        Stop();
        _capture.Dispose();
        _output.Dispose();
    }

    /// <summary>Sets a band's gain, persisting across capture/output restarts.</summary>
    public void SetBandGain(int bandIndex, float gainDecibels)
    {
        lock (_lock)
        {
            _bandGains[bandIndex] = gainDecibels;
            if (Equalizer is not null)
            {
                Equalizer.Bands[bandIndex].GainDecibels = gainDecibels;
            }
        }
    }

    private void ReconnectOutput()
    {
        lock (_lock)
        {
            if (!IsRunning)
            {
                return;
            }

            // capture.Samples is a new instance after a restart - re-Init
            // output against it (via a fresh equalizer) rather than let it
            // keep pulling a dead one.
            _output.Stop();
            StartOutputFromCapture();
        }
    }

    private void StartOutputFromCapture()
    {
        var equalizer = new GraphicEqualizer(_capture.Samples!);
        for (var i = 0; i < _bandGains.Length; i++)
        {
            equalizer.Bands[i].GainDecibels = _bandGains[i];
        }

        Equalizer = equalizer;
        _output.Start(equalizer, _outputDeviceId);
    }
}
