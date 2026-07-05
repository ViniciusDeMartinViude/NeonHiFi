using System.Configuration;
using System.Data;
using System.Windows;
using NeonHiFi.App.Settings;

namespace NeonHiFi.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = SettingsService.Load();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsService.Save(Settings);
        base.OnExit(e);
    }
}

