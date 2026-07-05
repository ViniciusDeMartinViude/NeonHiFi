using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using NeonHiFi.App.Logging;
using NeonHiFi.App.Settings;
using NeonHiFi.Audio.Devices;
using Serilog;

namespace NeonHiFi.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private AudioDeviceWatcher? _deviceWatcher;

    public AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogging.Configure();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Log.Information("NeonHiFi starting up");

        _deviceWatcher = new AudioDeviceWatcher();

        base.OnStartup(e);
        Settings = SettingsService.Load();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsService.Save(Settings);

        _deviceWatcher?.Dispose();

        Log.Information("NeonHiFi shutting down");
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled exception on the UI thread");
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}

