using NeonHiFi.Audio.Dsp;

namespace NeonHiFi.App.Settings;

/// <summary>
/// Save/load for user EQ presets, layered on top of the built-in ones.
/// Built-in names (Flat, Rock, Bass Boost, Vocal) are reserved: saving under
/// one throws, so a user can never accidentally overwrite a built-in via
/// save-as - they have to pick a different name.
/// </summary>
public sealed class EqPresetService
{
    private readonly AppSettings _settings;

    public EqPresetService(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>Built-in presets followed by the user's saved ones.</summary>
    public IReadOnlyList<EqPreset> GetAllPresets()
    {
        var presets = new List<EqPreset>(BuiltInEqPresets.All);
        foreach (var (name, gains) in _settings.UserEqPresets)
        {
            presets.Add(new EqPreset(name, gains));
        }

        return presets;
    }

    public EqPreset? GetPreset(string name)
    {
        var builtIn = BuiltInEqPresets.All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (builtIn is not null)
        {
            return builtIn;
        }

        return _settings.UserEqPresets.TryGetValue(name, out var gains) ? new EqPreset(name, gains) : null;
    }

    /// <summary>Saves (or overwrites) a user preset. Throws if <paramref name="name"/> collides with a built-in.</summary>
    public void SavePreset(string name, IReadOnlyList<float> bandGains)
    {
        if (BuiltInEqPresets.IsReservedName(name))
        {
            throw new InvalidOperationException($"\"{name}\" is a built-in preset name and can't be overwritten. Save as a different name instead.");
        }

        _settings.UserEqPresets[name] = bandGains.ToArray();
        SettingsService.Save(_settings);
    }

    /// <summary>
    /// Removes a user preset, if one exists under that name. Built-in presets
    /// were never stored in UserEqPresets in the first place (SavePreset
    /// rejects reserved names), so this can never delete one.
    /// </summary>
    public void DeletePreset(string name)
    {
        if (_settings.UserEqPresets.Remove(name))
        {
            SettingsService.Save(_settings);
        }
    }
}
