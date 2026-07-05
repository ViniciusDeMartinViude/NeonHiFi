using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NeonHiFi.App.Settings;
using NeonHiFi.App.ViewModels;
using NeonHiFi.Audio.Dsp;
using NeonHiFi.Audio.Pipeline;

namespace NeonHiFi.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly Brush _powerLedOffBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x10, 0x10));
    private static readonly Brush _powerLedOnBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xFF, 0x7A));

    private readonly AudioPipeline _pipeline = new();
    private readonly EqPresetService _presetService;
    private readonly List<Slider> _eqSliders = [];
    private VisualizationViewModel? _visualizationViewModel;
    private bool _isPoweredOn;

    public MainWindow()
    {
        InitializeComponent();

        var settings = ((App)Application.Current).Settings;
        _presetService = new EqPresetService(settings);

        ApplySettings();
        Closing += (_, _) => OnClosing();
        BuildEqBandSliders();

        _visualizationViewModel = new VisualizationViewModel(_pipeline);
        DataContext = _visualizationViewModel;

        PresetSelector.ItemsSource = _presetService.GetAllPresets().Select(p => p.Name).ToList();
        PresetSelector.SelectedItem = settings.EqPresetName;

        // Starts powered off - a real hi-fi doesn't come alive the moment you
        // plug it in. The controls stay wired to the pipeline's stored gains
        // (set via the preset selection above) so the first power-on already
        // reflects the last-used preset.
        SetPoweredOn(false, animateOn: false);
    }

    private void PowerButton_Click(object sender, RoutedEventArgs e) => SetPoweredOn(!_isPoweredOn, animateOn: true);

    /// <summary>
    /// Powers the whole pipeline on/off. Off stops capture+output entirely -
    /// VisualizationViewModel resets the VU/spectrum to idle once it notices
    /// (see its IsRunning check) - and disables the EQ/DSP controls so
    /// there's no ambiguity that they're inert. On restarts the pipeline
    /// (which already remembers prior gains/effect settings - those aren't
    /// reset by Stop()) and, if requested, plays the LED-marquee animation
    /// while audio resumes immediately underneath it. The marquee turns
    /// itself off once it's scrolled fully across (SpectrumDisplay owns that
    /// timing), so there's no fixed duration to guess here.
    /// </summary>
    private void SetPoweredOn(bool poweredOn, bool animateOn)
    {
        _isPoweredOn = poweredOn;
        PowerButton.IsChecked = poweredOn;
        PowerLed.Fill = poweredOn ? _powerLedOnBrush : _powerLedOffBrush;
        SetEqDspControlsEnabled(poweredOn);

        if (poweredOn)
        {
            _pipeline.Start(((App)Application.Current).Settings.SelectedOutputDeviceId);

            if (animateOn)
            {
                SpectrumDisplayControl.MarqueeText = ((App)Application.Current).Settings.PowerOnMessage;
                SpectrumDisplayControl.IsMarqueeActive = true;
            }
        }
        else
        {
            SpectrumDisplayControl.IsMarqueeActive = false;
            _pipeline.Stop();
        }
    }

    private void SetEqDspControlsEnabled(bool isEnabled)
    {
        PresetSelector.IsEnabled = isEnabled;
        BassBoostToggle.IsEnabled = isEnabled;
        StereoWidthToggle.IsEnabled = isEnabled;
        WarmthToggle.IsEnabled = isEnabled;

        foreach (var slider in _eqSliders)
        {
            slider.IsEnabled = isEnabled;
        }
    }

    private void PresetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetSelector.SelectedItem is not string name)
        {
            return;
        }

        var preset = _presetService.GetPreset(name);
        if (preset is null)
        {
            return;
        }

        for (var i = 0; i < preset.BandGains.Count && i < _eqSliders.Count; i++)
        {
            _eqSliders[i].Value = preset.BandGains[i];
        }

        _pipeline.ApplyPreset(preset);
        ((App)Application.Current).Settings.EqPresetName = name;
    }

    private void DspEffectToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle)
        {
            return;
        }

        var isEnabled = toggle.IsChecked == true;

        if (toggle == BassBoostToggle)
        {
            _pipeline.SetBassBoost(isEnabled, 9f);
        }
        else if (toggle == StereoWidthToggle)
        {
            _pipeline.SetStereoWidth(isEnabled, 1.6f);
        }
        else if (toggle == WarmthToggle)
        {
            _pipeline.SetWarmth(isEnabled, 0.6f);
        }
    }

    private void ApplySettings()
    {
        var settings = ((App)Application.Current).Settings;

        Width = settings.WindowWidth;

        // Height is intentionally not restored here: the window uses
        // SizeToContent="Height" so its height always matches the actual
        // (Auto-sized) content rather than a persisted value that could
        // drift from the current layout.
        if (settings.WindowLeft is double left && settings.WindowTop is double top)
        {
            Left = left;
            Top = top;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        // Restore whatever the window ended up as, but never reopen already minimized.
        WindowState = settings.WindowState == WindowState.Minimized ? WindowState.Normal : settings.WindowState;
    }

    private void OnClosing()
    {
        SaveWindowState();
        _visualizationViewModel?.Dispose();
        _pipeline.Dispose();
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

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// One vertical slider per graphic EQ band, styled via FaderSliderStyle to
    /// look like a real mixing-console fader (see MainWindow.xaml), wired
    /// live to the pipeline.
    /// </summary>
    private void BuildEqBandSliders()
    {
        var faderStyle = (Style)Resources["FaderSliderStyle"];

        foreach (var frequency in GraphicEqualizer.StandardCenterFrequencies)
        {
            var bandIndex = _eqSliders.Count;
            var slider = new Slider
            {
                Style = faderStyle,
                Orientation = Orientation.Vertical,
                Minimum = -12,
                Maximum = 12,
                Value = 0,
                Height = 260,
                Width = 40,
                TickFrequency = 3,
                IsSnapToTickEnabled = false,
            };
            slider.ValueChanged += (_, e) => _pipeline.SetBandGain(bandIndex, (float)e.NewValue);
            _eqSliders.Add(slider);

            var label = new TextBlock
            {
                Text = FormatFrequencyLabel(frequency),
                Foreground = Brushes.Gainsboro,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
            };

            var column = new StackPanel { Margin = new Thickness(10, 0, 10, 0) };
            column.Children.Add(slider);
            column.Children.Add(label);

            EqBandsPanel.Children.Add(column);
        }
    }

    private static string FormatFrequencyLabel(double frequencyHz) =>
        frequencyHz >= 1000 ? $"{frequencyHz / 1000:0.#}k" : $"{frequencyHz:0}";
}
