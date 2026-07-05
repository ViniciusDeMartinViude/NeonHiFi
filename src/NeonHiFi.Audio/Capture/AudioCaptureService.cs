using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using Serilog;

namespace NeonHiFi.Audio.Capture;

/// <summary>
/// Captures the system's current audio output (WASAPI loopback on the default
/// render device) and exposes it as a pull-based <see cref="ISampleProvider"/>
/// for downstream DSP/visualization consumers. Automatically restarts capture
/// against the new default device if the user changes it (or unplugs the
/// current one) mid-capture, instead of crashing or silently going dead.
/// </summary>
public sealed class AudioCaptureService : IDisposable
{
    private readonly object _lock = new();
    private WasapiLoopbackCapture? _capture;
    private DefaultRenderDeviceListener? _deviceListener;
    private bool _stopRequested = true;

    public bool IsCapturing { get; private set; }

    public WaveFormat? WaveFormat => _capture?.WaveFormat;

    public ISampleProvider? Samples { get; private set; }

    /// <summary>Raised when capture had to restart (device change or capture error). Never fatal.</summary>
    public event EventHandler<Exception?>? CaptureRestarted;

    public void Start()
    {
        lock (_lock)
        {
            if (IsCapturing)
            {
                return;
            }

            _stopRequested = false;
            _deviceListener ??= new DefaultRenderDeviceListener(OnDefaultRenderDeviceChanged);
            StartCaptureInternal();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _stopRequested = true;
            StopCaptureInternal();
            _deviceListener?.Dispose();
            _deviceListener = null;
        }
    }

    public void Dispose() => Stop();

    private void StartCaptureInternal()
    {
        var capture = new WasapiLoopbackCapture();
        var bufferedProvider = new BufferedWaveProvider(capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2),
        };

        capture.DataAvailable += (_, e) => bufferedProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        capture.RecordingStopped += OnRecordingStopped;

        capture.StartRecording();

        _capture = capture;
        Samples = bufferedProvider.ToSampleProvider();
        IsCapturing = true;

        Log.Information(
            "Audio capture started ({SampleRate} Hz, {Channels} ch, {BitsPerSample}-bit)",
            capture.WaveFormat.SampleRate, capture.WaveFormat.Channels, capture.WaveFormat.BitsPerSample);
    }

    private void StopCaptureInternal()
    {
        var capture = _capture;
        if (capture is null)
        {
            return;
        }

        _capture = null;
        Samples = null;
        IsCapturing = false;

        capture.RecordingStopped -= OnRecordingStopped;
        try
        {
            capture.StopRecording();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Ignoring error stopping an already-broken capture device");
        }

        capture.Dispose();
        Log.Information("Audio capture stopped");
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            if (_stopRequested)
            {
                return;
            }

            if (e.Exception is not null)
            {
                Log.Warning(e.Exception, "Audio capture stopped unexpectedly, restarting");
            }

            RestartCapture(e.Exception);
        }
    }

    private void OnDefaultRenderDeviceChanged()
    {
        lock (_lock)
        {
            if (_stopRequested || !IsCapturing)
            {
                return;
            }

            Log.Information("Default audio output device changed, restarting capture");
            RestartCapture(exception: null);
        }
    }

    private void RestartCapture(Exception? exception)
    {
        StopCaptureInternal();

        try
        {
            StartCaptureInternal();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to restart audio capture after device change");
            exception = ex;
        }

        CaptureRestarted?.Invoke(this, exception);
    }

    /// <summary>
    /// Restarts capture whenever the default render (playback) device changes,
    /// since a <see cref="WasapiLoopbackCapture"/> instance is bound to whichever
    /// device was default at construction time and won't follow the switch on its own.
    /// </summary>
    private sealed class DefaultRenderDeviceListener : IMMNotificationClient, IDisposable
    {
        private readonly MMDeviceEnumerator _enumerator = new();
        private readonly Action _onDefaultRenderDeviceChanged;

        public DefaultRenderDeviceListener(Action onDefaultRenderDeviceChanged)
        {
            _onDefaultRenderDeviceChanged = onDefaultRenderDeviceChanged;
            _enumerator.RegisterEndpointNotificationCallback(this);
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
        }

        public void OnDeviceRemoved(string deviceId)
        {
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Console)
            {
                _onDefaultRenderDeviceChanged();
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
        }

        public void Dispose()
        {
            _enumerator.UnregisterEndpointNotificationCallback(this);
            _enumerator.Dispose();
        }
    }
}
