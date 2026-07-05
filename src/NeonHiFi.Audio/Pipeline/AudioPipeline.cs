using NeonHiFi.Audio.Capture;
using NeonHiFi.Audio.Output;

namespace NeonHiFi.Audio.Pipeline;

/// <summary>
/// Owns the full capture -> (future DSP) -> output chain as a single unit.
/// A future EQ/effects stage slots in between capture and output inside
/// <see cref="ReconnectOutput"/> once it exists; today the chain is a
/// straight passthrough via NAudio's ISampleProvider/BufferedWaveProvider
/// (the "buffered provider" option this issue calls out as an alternative
/// to a hand-rolled ring buffer).
///
/// Both underlying services can restart independently (capture on a
/// default-device change, output on playback failure) - this class keeps
/// them in sync, since capture handing out a *new* ISampleProvider after a
/// restart would otherwise leave a running output stuck reading a dead one.
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
    private string? _outputDeviceId;

    public bool IsRunning { get; private set; }

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
            _output.Start(_capture.Samples!, _outputDeviceId);
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
            IsRunning = false;
        }
    }

    public void Dispose()
    {
        Stop();
        _capture.Dispose();
        _output.Dispose();
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
            // output against it rather than let it keep pulling a dead one.
            _output.Stop();
            _output.Start(_capture.Samples!, _outputDeviceId);
        }
    }
}
