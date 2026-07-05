using System.Windows.Media;
using NeonHiFi.Audio.Pipeline;

namespace NeonHiFi.App.ViewModels;

/// <summary>
/// Polls an AudioPipeline's SpectrumAnalyzer/VuMeter once per composed frame
/// via CompositionTarget.Rendering - which ties updates to the display's
/// actual refresh rate (typically 60Hz) rather than an arbitrary timer
/// interval - and exposes the results as bindable properties for Phase 3's
/// UI controls.
///
/// This is deliberately decoupled from the audio thread in both directions:
/// GetLatestMagnitudesDb()/GetNeedleLevels()/GetPeakLevels() are thread-safe
/// snapshot reads (Volatile.Read under the hood) that never block, and this
/// class only ever calls them from the UI thread's own render callback - it
/// never waits on, or can be waited on by, the real-time audio path. Reading
/// a snapshot mid-update just means this frame shows last frame's data,
/// never a torn read or a stall.
/// </summary>
public sealed class VisualizationViewModel : ViewModelBase, IDisposable
{
    private const float IdleMagnitudeDb = -100f;
    private const int DefaultBandCountBeforeFirstFrame = 24;

    private readonly AudioPipeline _pipeline;

    private IReadOnlyList<float> _magnitudes = Array.Empty<float>();
    private float _leftLevel;
    private float _leftPeak;
    private float _rightLevel;
    private float _rightPeak;
    private bool _wasRunning;

    public VisualizationViewModel(AudioPipeline pipeline)
    {
        _pipeline = pipeline;

        // Without this, Magnitudes stays at its Array.Empty<float>() default
        // until the pipeline has been started and stopped once - the
        // running-to-stopped reset below never fires on a fresh launch,
        // since _wasRunning starts false and the pipeline starts stopped too.
        // That left the spectrum with no bars to draw at all (segment count
        // of zero), rendering as a blank screen instead of the unlit LED grid.
        Magnitudes = Enumerable.Repeat(IdleMagnitudeDb, DefaultBandCountBeforeFirstFrame).ToArray();

        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>Latest spectrum band magnitudes (dB), for binding to a spectrum display control.</summary>
    public IReadOnlyList<float> Magnitudes
    {
        get => _magnitudes;
        private set => SetProperty(ref _magnitudes, value);
    }

    public float LeftLevel
    {
        get => _leftLevel;
        private set => SetProperty(ref _leftLevel, value);
    }

    public float LeftPeak
    {
        get => _leftPeak;
        private set => SetProperty(ref _leftPeak, value);
    }

    public float RightLevel
    {
        get => _rightLevel;
        private set => SetProperty(ref _rightLevel, value);
    }

    public float RightPeak
    {
        get => _rightPeak;
        private set => SetProperty(ref _rightPeak, value);
    }

    public void Dispose()
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_pipeline.IsRunning)
        {
            // Only reset once per power-off, not every frame while off -
            // otherwise this would fight a UI that wants to briefly show
            // something else (e.g. the marquee) while powered down.
            if (_wasRunning)
            {
                var bandCount = _magnitudes.Count > 0 ? _magnitudes.Count : DefaultBandCountBeforeFirstFrame;
                Magnitudes = Enumerable.Repeat(IdleMagnitudeDb, bandCount).ToArray();
                LeftLevel = 0f;
                LeftPeak = 0f;
                RightLevel = 0f;
                RightPeak = 0f;
                _wasRunning = false;
            }

            return;
        }

        _wasRunning = true;

        var spectrum = _pipeline.SpectrumAnalyzer?.GetLatestMagnitudesDb();
        if (spectrum is not null)
        {
            Magnitudes = spectrum;
        }

        var needles = _pipeline.VuMeter?.GetNeedleLevels();
        var peaks = _pipeline.VuMeter?.GetPeakLevels();
        if (needles is { Length: >= 2 } && peaks is { Length: >= 2 })
        {
            LeftLevel = needles[0];
            LeftPeak = peaks[0];
            RightLevel = needles[1];
            RightPeak = peaks[1];
        }
    }
}
