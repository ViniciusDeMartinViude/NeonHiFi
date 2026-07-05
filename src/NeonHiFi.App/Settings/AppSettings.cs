using System.Windows;

namespace NeonHiFi.App.Settings;

public class AppSettings
{
    public string? SelectedOutputDeviceId { get; set; }

    public string EqPresetName { get; set; } = "Flat";

    /// <summary>User-saved EQ presets, keyed by name. Never contains a reserved built-in name - see EqPresetService.SavePreset.</summary>
    public Dictionary<string, float[]> UserEqPresets { get; set; } = new();

    /// <summary>Text shown in the power-on LED marquee. Only characters in DotMatrixFont render; others show as a blank gap.</summary>
    public string PowerOnMessage { get; set; } = "Neon HiFi";

    public double WindowWidth { get; set; } = 800;

    public double WindowHeight { get; set; } = 450;

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public WindowState WindowState { get; set; } = WindowState.Normal;
}
