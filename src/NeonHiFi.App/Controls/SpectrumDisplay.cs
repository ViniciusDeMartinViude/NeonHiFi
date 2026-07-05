using System.Windows;
using System.Windows.Media;

namespace NeonHiFi.App.Controls;

/// <summary>
/// An 80s-style segmented LED bar-graph "screen": renders an arbitrary set of
/// magnitude values (e.g. FFT spectrum bands) as vertical bars, green at the
/// bottom shading through yellow to red at the top - the classic hi-fi
/// equalizer look. Purely a rendering component: it has no idea the values
/// it's given came from an FFT or anything else audio-related, it just draws
/// whatever's in <see cref="Magnitudes"/> scaled between <see
/// cref="MinValue"/> and <see cref="MaxValue"/>.
///
/// A custom FrameworkElement drawing directly in OnRender (rather than a
/// templated Control with a Rectangle per LED segment) keeps this cheap
/// enough for smooth 30-60fps updates - a handful of DrawRectangle calls per
/// frame, no per-frame allocation (brushes are frozen once, statically).
/// </summary>
public sealed class SpectrumDisplay : FrameworkElement
{
    public static readonly DependencyProperty MagnitudesProperty = DependencyProperty.Register(
        nameof(Magnitudes),
        typeof(IReadOnlyList<float>),
        typeof(SpectrumDisplay),
        new FrameworkPropertyMetadata(Array.Empty<float>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register(
        nameof(MinValue),
        typeof(double),
        typeof(SpectrumDisplay),
        new FrameworkPropertyMetadata(-80.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
        nameof(MaxValue),
        typeof(double),
        typeof(SpectrumDisplay),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SegmentCountProperty = DependencyProperty.Register(
        nameof(SegmentCount),
        typeof(int),
        typeof(SpectrumDisplay),
        new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush _backgroundBrush = Freeze(Color.FromRgb(8, 12, 8));
    private static readonly Brush _unlitSegmentBrush = Freeze(Color.FromRgb(20, 30, 20));
    private static readonly Brush _greenSegmentBrush = Freeze(Color.FromRgb(40, 220, 90));
    private static readonly Brush _yellowSegmentBrush = Freeze(Color.FromRgb(230, 210, 40));
    private static readonly Brush _redSegmentBrush = Freeze(Color.FromRgb(230, 60, 50));

    public IReadOnlyList<float> Magnitudes
    {
        get => (IReadOnlyList<float>)GetValue(MagnitudesProperty);
        set => SetValue(MagnitudesProperty, value);
    }

    /// <summary>The value that maps to zero lit segments.</summary>
    public double MinValue
    {
        get => (double)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    /// <summary>The value that maps to all segments lit.</summary>
    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>How many LED segments tall each bar is.</summary>
    public int SegmentCount
    {
        get => (int)GetValue(SegmentCountProperty);
        set => SetValue(SegmentCountProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        drawingContext.DrawRectangle(_backgroundBrush, null, new Rect(0, 0, width, height));

        var magnitudes = Magnitudes;
        var barCount = magnitudes.Count;
        if (barCount == 0)
        {
            return;
        }

        const double BarGapFraction = 0.2;
        const double SegmentGapFraction = 0.15;

        var barSlot = width / barCount;
        var barWidth = barSlot * (1 - BarGapFraction);
        var segmentCount = Math.Max(1, SegmentCount);
        var segmentSlot = height / segmentCount;
        var segmentHeight = segmentSlot * (1 - SegmentGapFraction);
        var range = MaxValue - MinValue;

        for (var bar = 0; bar < barCount; bar++)
        {
            var normalized = range > 0 ? (magnitudes[bar] - MinValue) / range : 0;
            normalized = Math.Clamp(normalized, 0, 1);
            var litSegments = (int)Math.Round(normalized * segmentCount);

            var x = (bar * barSlot) + ((barSlot - barWidth) / 2);

            for (var segment = 0; segment < segmentCount; segment++)
            {
                var y = height - ((segment + 1) * segmentSlot) + (segmentSlot - segmentHeight);
                var brush = segment < litSegments ? BrushForSegment(segment, segmentCount) : _unlitSegmentBrush;
                drawingContext.DrawRectangle(brush, null, new Rect(x, y, barWidth, segmentHeight));
            }
        }
    }

    private static Brush BrushForSegment(int segmentIndex, int segmentCount)
    {
        var fraction = (double)segmentIndex / segmentCount;
        return fraction switch
        {
            >= 0.9 => _redSegmentBrush,
            >= 0.7 => _yellowSegmentBrush,
            _ => _greenSegmentBrush,
        };
    }

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
