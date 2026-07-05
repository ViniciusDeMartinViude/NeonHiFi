using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NeonHiFi.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplySettings();
        Closing += (_, _) => SaveWindowState();
    }

    private void ApplySettings()
    {
        var settings = ((App)Application.Current).Settings;

        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        if (settings.WindowLeft is double left && settings.WindowTop is double top)
        {
            Left = left;
            Top = top;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        // Restore whatever the window ended up as, but never reopen already minimized.
        WindowState = settings.WindowState == WindowState.Minimized ? WindowState.Normal : settings.WindowState;
    }

    private void SaveWindowState()
    {
        var settings = ((App)Application.Current).Settings;

        // Persist the restored (non-maximized) bounds so a maximized window doesn't
        // permanently overwrite the "normal" size/position the user last chose.
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

        settings.WindowWidth = bounds.Width;
        settings.WindowHeight = bounds.Height;
        settings.WindowLeft = bounds.X;
        settings.WindowTop = bounds.Y;
        settings.WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
    }
}
