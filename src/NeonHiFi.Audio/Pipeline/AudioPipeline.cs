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
/// The chain after the EQ is BassBoost -> StereoWidth -> Warmth -> output:
/// tone-shaping (EQ, then the dedicated bass shelf) happens before stereo
/// imaging, which happens before saturation is added to the final shaped/
/// widened signal. All three are optional stretch effects, disabled by
/// default (Enabled = false, an exact passthrough) - the app works fully
/// with just the EQ without ever touching them.
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

    private bool _bassBoostEnabled;
    private float _bassBoostGainDb;
    private bool _stereoWidthEnabled;
    private float _stereoWidthValue = 1f;
    private bool _warmthEnabled;
    private float _warmthAmount;

    public bool IsRunning { get; private set; }

    public GraphicEqualizer? Equalizer { get; private set; }

    public BassBoostEffect? BassBoost { get; private set; }

    public StereoWidthEffect? StereoWidth { get; private set; }

    public WarmthEffect? Warmth { get; private set; }

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
            BassBoost = null;
            StereoWidth = null;
            Warmth = null;
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

    /// <summary>Applies every band gain from an <see cref="EqPreset"/> in one go.</summary>
    public void ApplyPreset(EqPreset preset)
    {
        for (var i = 0; i < preset.BandGains.Count && i < _bandGains.Length; i++)
        {
            SetBandGain(i, preset.BandGains[i]);
        }
    }

    /// <summary>Enables/adjusts the bass boost shelf, persisting across capture/output restarts.</summary>
    public void SetBassBoost(bool enabled, float gainDecibels)
    {
        lock (_lock)
        {
            _bassBoostEnabled = enabled;
            _bassBoostGainDb = gainDecibels;
            if (BassBoost is not null)
            {
                BassBoost.Enabled = enabled;
                BassBoost.GainDecibels = gainDecibels;
            }
        }
    }

    /// <summary>Enables/adjusts stereo widening, persisting across capture/output restarts.</summary>
    public void SetStereoWidth(bool enabled, float width)
    {
        lock (_lock)
        {
            _stereoWidthEnabled = enabled;
            _stereoWidthValue = width;
            if (StereoWidth is not null)
            {
                StereoWidth.Enabled = enabled;
                StereoWidth.Width = width;
            }
        }
    }

    /// <summary>Enables/adjusts the warmth (saturation) effect, persisting across capture/output restarts.</summary>
    public void SetWarmth(bool enabled, float amount)
    {
        lock (_lock)
        {
            _warmthEnabled = enabled;
            _warmthAmount = amount;
            if (Warmth is not null)
            {
                Warmth.Enabled = enabled;
                Warmth.Amount = amount;
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

        var bassBoost = new BassBoostEffect(equalizer) { Enabled = _bassBoostEnabled, GainDecibels = _bassBoostGainDb };
        var stereoWidth = new StereoWidthEffect(bassBoost) { Enabled = _stereoWidthEnabled, Width = _stereoWidthValue };
        var warmth = new WarmthEffect(stereoWidth) { Enabled = _warmthEnabled, Amount = _warmthAmount };

        Equalizer = equalizer;
        BassBoost = bassBoost;
        StereoWidth = stereoWidth;
        Warmth = warmth;

        _output.Start(warmth, _outputDeviceId);
    }
}
