using System.IO;
using Serilog;

namespace NeonHiFi.App.Logging;

/// <summary>
/// Configures the process-wide Serilog logger, writing to %AppData%/NeonHiFi/logs/.
/// The file sink is wrapped in Serilog.Sinks.Async so a call from the wrong place
/// (e.g. accidentally from the audio callback thread) can't block on file I/O -
/// see CLAUDE.md's real-time audio conventions.
/// </summary>
public static class AppLogging
{
    public static void Configure()
    {
        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NeonHiFi", "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Async(sink => sink.File(
                Path.Combine(logsDirectory, "neonhifi-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14))
            .CreateLogger();
    }
}
