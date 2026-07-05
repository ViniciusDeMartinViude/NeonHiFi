using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeonHiFi.App.Settings;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> from %AppData%/NeonHiFi/settings.json.
/// Never throws: a missing or corrupted file yields defaults, and a failed
/// save is logged rather than propagated, so settings issues never crash the app.
/// </summary>
public static class SettingsService
{
    private static readonly string _settingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NeonHiFi");

    private static readonly string _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"NeonHiFi: failed to load settings, using defaults ({ex.Message}).");
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            var json = JsonSerializer.Serialize(settings, _jsonOptions);

            // Write to a temp file and swap it in, so a crash mid-write can't corrupt the real file.
            var tempFilePath = _settingsFilePath + ".tmp";
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _settingsFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"NeonHiFi: failed to save settings ({ex.Message}).");
        }
    }
}
