using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;

namespace NeonHiFi.Audio.Output;

/// <summary>
/// Renders a processed audio stream to a user-selected WASAPI output device
/// (typically headphones) via NAudio's WasapiOut.
/// </summary>
public sealed class AudioOutputService : IDisposable
{
    // Polling (non-event-driven) mode with a moderate buffer favors glitch-free
    // playback over minimal latency - appropriate here since NeonHiFi is a
    // listening enhancer, not a tracking instrument needing sub-10ms latency.
    private const int LatencyMilliseconds = 200;

    private readonly object _lock = new();
    private WasapiOut? _output;
    private bool _stopRequested = true;

    public bool IsPlaying { get; private set; }

    /// <summary>Raised when playback stopped other than via an explicit <see cref="Stop"/> call.</summary>
    public event EventHandler<Exception?>? PlaybackStopped;

    public static IReadOnlyList<AudioDeviceInfo> GetAvailableDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        var result = new List<AudioDeviceInfo>();
        foreach (var device in devices)
        {
            result.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
            device.Dispose();
        }

        return result;
    }

    public void Start(ISampleProvider source, string? deviceId = null)
    {
        lock (_lock)
        {
            if (IsPlaying)
            {
                return;
            }

            _stopRequested = false;

            using var device = ResolveDevice(deviceId);
            var output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: false, LatencyMilliseconds);

            output.PlaybackStopped += OnPlaybackStopped;
            output.Init(source);
            output.Play();

            _output = output;
            IsPlaying = true;

            Log.Information("Audio output started on {Device}", device.FriendlyName);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _stopRequested = true;
            StopInternal();
        }
    }

    public void Dispose() => Stop();

    private void StopInternal()
    {
        var output = _output;
        if (output is null)
        {
            return;
        }

        _output = null;
        IsPlaying = false;

        output.PlaybackStopped -= OnPlaybackStopped;
        try
        {
            output.Stop();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Ignoring error stopping an already-broken output device");
        }

        output.Dispose();
        Log.Information("Audio output stopped");
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            if (_stopRequested)
            {
                return;
            }

            if (e.Exception is not null)
            {
                Log.Warning(e.Exception, "Audio output stopped unexpectedly (device disconnected?)");
            }

            StopInternal();
            PlaybackStopped?.Invoke(this, e.Exception);
        }
    }

    private static MMDevice ResolveDevice(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();

        if (deviceId is not null)
        {
            try
            {
                return enumerator.GetDevice(deviceId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Requested output device {DeviceId} unavailable, falling back to default", deviceId);
            }
        }

        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
    }
}
