namespace NeonHiFi.Audio.Dsp;

/// <summary>
/// Built-in graphic EQ presets, one gain per <see cref="GraphicEqualizer.StandardCenterFrequencies"/>
/// band (31, 62, 125, 250, 500, 1k, 2k, 4k, 8k, 16k Hz). These names are
/// reserved: user-saved presets can't use them, so a save-as of the same
/// name can never clobber a built-in (see EqPresetService.SavePreset).
/// </summary>
public static class BuiltInEqPresets
{
    public static readonly EqPreset Flat = new("Flat", new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

    public static readonly EqPreset Rock = new("Rock", new float[] { 5, 4, 3, 0, -2, -3, 0, 2, 4, 5 });

    public static readonly EqPreset BassBoost = new("Bass Boost", new float[] { 7, 6, 5, 3, 1, 0, 0, 0, 0, 0 });

    public static readonly EqPreset Vocal = new("Vocal", new float[] { -3, -3, -2, 1, 3, 4, 3, 1, -1, -2 });

    public static readonly IReadOnlyList<EqPreset> All = [Flat, Rock, BassBoost, Vocal];

    public static bool IsReservedName(string name) =>
        All.Any(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
}
