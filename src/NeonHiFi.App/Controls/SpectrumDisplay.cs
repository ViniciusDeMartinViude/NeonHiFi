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
///
/// While <see cref="IsMarqueeActive"/>, the same LED grid renders <see
/// cref="MarqueeText"/> scrolling right-to-left instead of magnitude bars -
/// each letter is a <see cref="DotMatrixFont"/> glyph lit up cell-by-cell on
/// the identical segment grid, not an overlaid image/vector font, so it
/// reads as the same physical display forming letters rather than something
/// drawn on top of it. The message enters fully off-screen right, crosses
/// once, and exits fully off-screen left - no looping - then the control
/// turns the marquee off itself (see <see cref="OnMarqueeRendering"/>).
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

    public static readonly DependencyProperty MarqueeTextProperty = DependencyProperty.Register(
        nameof(MarqueeText),
        typeof(string),
        typeof(SpectrumDisplay),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsMarqueeActiveProperty = DependencyProperty.Register(
        nameof(IsMarqueeActive),
        typeof(bool),
        typeof(SpectrumDisplay),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsMarqueeActiveChanged));

    private const int GlyphGapColumns = 1;
    private const double MarqueeColumnsPerSecond = 28;

    private static readonly Brush _backgroundBrush = Freeze(Color.FromRgb(8, 12, 8));
    private static readonly Brush _unlitSegmentBrush = Freeze(Color.FromRgb(20, 30, 20));
    private static readonly Brush _greenSegmentBrush = Freeze(Color.FromRgb(40, 220, 90));
    private static readonly Brush _yellowSegmentBrush = Freeze(Color.FromRgb(230, 210, 40));
    private static readonly Brush _redSegmentBrush = Freeze(Color.FromRgb(230, 60, 50));

    private bool[,]? _marqueeBitmap;
    private int _marqueeBitmapWidth;
    private int _marqueeVisibleColumns;
    private double _marqueeColumnOffset;
    private DateTime _lastMarqueeRenderTime;

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

    /// <summary>How many LED segments tall each bar (or, in marquee mode, each column) is.</summary>
    public int SegmentCount
    {
        get => (int)GetValue(SegmentCountProperty);
        set => SetValue(SegmentCountProperty, value);
    }

    /// <summary>Text scrolled right-to-left, formed from lit LED cells, while <see cref="IsMarqueeActive"/> is true.</summary>
    public string MarqueeText
    {
        get => (string)GetValue(MarqueeTextProperty);
        set => SetValue(MarqueeTextProperty, value);
    }

    /// <summary>While true, renders the scrolling <see cref="MarqueeText"/> instead of the magnitude bars.</summary>
    public bool IsMarqueeActive
    {
        get => (bool)GetValue(IsMarqueeActiveProperty);
        set => SetValue(IsMarqueeActiveProperty, value);
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

        if (IsMarqueeActive)
        {
            DrawMarquee(drawingContext, width, height);
            return;
        }

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

    private void DrawMarquee(DrawingContext drawingContext, double width, double height)
    {
        if (_marqueeBitmap is null || _marqueeBitmapWidth == 0 || _marqueeVisibleColumns == 0)
        {
            return;
        }

        const double CellGapFraction = 0.15;

        var segmentCount = Math.Max(1, SegmentCount);
        var cellSize = height / segmentCount;
        var cellWidth = cellSize * (1 - CellGapFraction);
        var cellHeight = cellSize * (1 - CellGapFraction);

        // Vertically center the 7-row glyph within the taller segment grid.
        var rowOffset = (segmentCount - DotMatrixFont.GlyphRows) / 2;

        var startColumn = (int)_marqueeColumnOffset;

        for (var column = 0; column < _marqueeVisibleColumns; column++)
        {
            var bitmapColumn = startColumn + column;
            var x = column * cellSize;

            for (var row = 0; row < segmentCount; row++)
            {
                var glyphRow = row - rowOffset;
                var lit = bitmapColumn < _marqueeBitmapWidth && glyphRow >= 0 && glyphRow < DotMatrixFont.GlyphRows && _marqueeBitmap[bitmapColumn, glyphRow];

                var y = row * cellSize;
                var brush = lit ? _greenSegmentBrush : _unlitSegmentBrush;
                drawingContext.DrawRectangle(brush, null, new Rect(x, y, cellWidth, cellHeight));
            }
        }
    }

    /// <summary>
    /// Builds the scrollable bitmap: <paramref name="visibleColumns"/> blank
    /// columns, then the text, then <paramref name="visibleColumns"/> more
    /// blank columns - so scrolling linearly from column 0 to (width -
    /// visibleColumns) shows the message enter fully off-screen right,
    /// cross the display once, and exit fully off-screen left. No looping.
    /// </summary>
    private static bool[,] BuildMarqueeBitmap(string text, int visibleColumns, out int bitmapWidth)
    {
        var textColumns = 0;
        foreach (var _ in text)
        {
            textColumns += DotMatrixFont.GlyphColumns + GlyphGapColumns;
        }

        var totalColumns = visibleColumns + textColumns + visibleColumns;
        var bitmap = new bool[totalColumns, DotMatrixFont.GlyphRows];
        var column = visibleColumns;

        foreach (var character in text)
        {
            if (DotMatrixFont.Glyphs.TryGetValue(character, out var glyph))
            {
                for (var gx = 0; gx < DotMatrixFont.GlyphColumns; gx++)
                {
                    for (var gy = 0; gy < DotMatrixFont.GlyphRows; gy++)
                    {
                        bitmap[column + gx, gy] = glyph[gx, gy];
                    }
                }
            }

            column += DotMatrixFont.GlyphColumns + GlyphGapColumns;
        }

        bitmapWidth = totalColumns;
        return bitmap;
    }

    private static void OnIsMarqueeActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var display = (SpectrumDisplay)d;
        if ((bool)e.NewValue)
        {
            var segmentCount = Math.Max(1, display.SegmentCount);
            var cellSize = display.ActualHeight / segmentCount;
            display._marqueeVisibleColumns = cellSize > 0 ? Math.Max(1, (int)(display.ActualWidth / cellSize)) : 1;
            display._marqueeBitmap = BuildMarqueeBitmap(display.MarqueeText, display._marqueeVisibleColumns, out display._marqueeBitmapWidth);
            display._marqueeColumnOffset = 0;
            display._lastMarqueeRenderTime = DateTime.UtcNow;
            CompositionTarget.Rendering += display.OnMarqueeRendering;
        }
        else
        {
            CompositionTarget.Rendering -= display.OnMarqueeRendering;
        }
    }

    private void OnMarqueeRendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var deltaSeconds = (now - _lastMarqueeRenderTime).TotalSeconds;
        _lastMarqueeRenderTime = now;

        _marqueeColumnOffset += MarqueeColumnsPerSecond * deltaSeconds;

        var maxOffset = _marqueeBitmapWidth - _marqueeVisibleColumns;
        if (_marqueeColumnOffset >= maxOffset)
        {
            _marqueeColumnOffset = maxOffset;
            InvalidateVisual();

            // Scrolled fully off-screen - turn the marquee off ourselves
            // (via SetCurrentValue so we don't fight an external binding)
            // rather than keep looping or sitting blank forever.
            SetCurrentValue(IsMarqueeActiveProperty, false);
            return;
        }

        InvalidateVisual();
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
