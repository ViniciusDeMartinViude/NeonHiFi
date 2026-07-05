using System.Windows;

namespace NeonHiFi.App.Settings;

public class AppSettings
{
    public string? SelectedOutputDeviceId { get; set; }

    public string EqPresetName { get; set; } = "Flat";

    public double WindowWidth { get; set; } = 800;

    public double WindowHeight { get; set; } = 450;

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public WindowState WindowState { get; set; } = WindowState.Normal;
}
