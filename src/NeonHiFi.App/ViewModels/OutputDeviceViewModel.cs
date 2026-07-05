using System.Collections.ObjectModel;
using System.Windows;
using NeonHiFi.App.Settings;
using NeonHiFi.Audio.Devices;
using NeonHiFi.Audio.Output;

namespace NeonHiFi.App.ViewModels;

/// <summary>
/// Exposes the available WASAPI render devices for output selection, kept
/// current as devices are plugged/unplugged, and persists the user's choice
/// via <see cref="SettingsService"/>. Purely data/behavior - no styling or
/// layout, which is Phase 3's job.
/// </summary>
public sealed class OutputDeviceViewModel : ViewModelBase, IDisposable
{
    private readonly AppSettings _settings;
    private readonly AudioDeviceWatcher _watcher = new();
    private AudioDeviceInfo? _selectedDevice;
    private string? _preferredDeviceId;

    public ObservableCollection<AudioDeviceInfo> AvailableDevices { get; } = new();

    /// <summary>The currently resolved selection, or null if the preferred device isn't connected right now.</summary>
    public AudioDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _preferredDeviceId = value?.Id;
            if (SetProperty(ref _selectedDevice, value))
            {
                _settings.SelectedOutputDeviceId = _preferredDeviceId;
                SettingsService.Save(_settings);
            }
        }
    }

    public OutputDeviceViewModel(AppSettings settings)
    {
        _settings = settings;
        _preferredDeviceId = settings.SelectedOutputDeviceId;
        _watcher.DevicesChanged += (_, _) => RefreshDevices();
        RefreshDevices();
    }

    public void Dispose() => _watcher.Dispose();

    private void RefreshDevices()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            dispatcher.Invoke(Apply);
        }

        void Apply()
        {
            var devices = AudioOutputService.GetAvailableDevices();

            AvailableDevices.Clear();
            foreach (var device in devices)
            {
                AvailableDevices.Add(device);
            }

            // Re-resolve the preferred device against the current list without
            // touching the persisted preference itself - a device that's only
            // temporarily unplugged should be re-selected automatically if it
            // comes back, not forgotten.
            var resolved = devices.FirstOrDefault(d => d.Id == _preferredDeviceId);
            SetProperty(ref _selectedDevice, resolved, nameof(SelectedDevice));
        }
    }
}
