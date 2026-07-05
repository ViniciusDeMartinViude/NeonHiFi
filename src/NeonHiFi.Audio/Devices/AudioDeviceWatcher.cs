using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Serilog;

namespace NeonHiFi.Audio.Devices;

/// <summary>
/// Logs Windows audio device changes (plugged/unplugged, default device switched,
/// enabled/disabled) via the Core Audio API's notification callback. Callbacks
/// arrive on a COM-owned thread, never the real-time audio callback thread, so
/// logging directly from here is safe (see CLAUDE.md real-time audio conventions).
/// </summary>
public sealed class AudioDeviceWatcher : IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public AudioDeviceWatcher()
    {
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    /// <summary>
    /// Raised when the set of active devices may have changed (added, removed,
    /// or enabled/disabled) - a cue for consumers like a device-picker view
    /// model to re-enumerate. Not raised for default-device or property
    /// changes, since those don't affect which devices are listed.
    /// </summary>
    public event EventHandler? DevicesChanged;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        Log.Information("Audio device {DeviceId} state changed to {State}", deviceId, newState);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        Log.Information("Audio device added: {DeviceId}", pwstrDeviceId);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OnDeviceRemoved(string deviceId)
    {
        Log.Information("Audio device removed: {DeviceId}", deviceId);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) =>
        Log.Information("Default audio device changed ({Flow}/{Role}): {DeviceId}", flow, role, defaultDeviceId);

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        // Fires for every property (name, volume, etc.) on every device - too
        // noisy to log; the events above cover what we actually care about.
    }

    public void Dispose()
    {
        _enumerator.UnregisterEndpointNotificationCallback(this);
        _enumerator.Dispose();
    }
}
