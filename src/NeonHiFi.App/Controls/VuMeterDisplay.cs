using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace NeonHiFi.App.Controls;

/// <summary>
/// A single-channel analog-style VU meter: a pivoting needle sweeping across
/// a scale face, plus a small peak-hold dot - the classic hi-fi look, as
/// opposed to a flat digital bar. One instance represents one channel; place
/// two side by side for stereo.
///
/// Purely a rendering component: <see cref="Level"/>/<see cref="PeakLevel"/>
/// are plain 0-1 DependencyProperties (0 = needle fully left/quiet, 1 = fully
/// right/loud) - no reference to NeonHiFi.Audio, no notion of where those
/// values came from. The scale face's dB-ish labels are illustrative,
/// matching a classic VU meter's look, not a calibrated mapping of the 0-1
/// input.
///
/// The scale face (background, ticks, labels, red zone) rarely changes, so
/// it's drawn once into a cached DrawingGroup and re-blitted each frame;
/// only the needle and peak dot are redrawn fresh, keeping the per-frame
/// cost to two cheap draws instead of rebuilding text layout every time.
/// </summary>
public sealed class VuMeterDisplay : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level),
        typeof(double),
        typeof(VuMeterDisplay),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PeakLevelProperty = DependencyProperty.Register(
        nameof(PeakLevel),
        typeof(double),
        typeof(VuMeterDisplay),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double SweepStartDegrees = -45;
    private const double SweepEndDegrees = 45;
    private const double RedZoneStartPosition = 0.83;

    // All tick marks (lines), matching a real VU meter's denser marking near
    // 0dB. Only a subset carries a text label - at this control's typical
    // size, labeling every mark (particularly -1/+1/+2, tightly packed near
    // the top of the sweep) made the text overlap illegibly.
    private static readonly (double Position, string? Label)[] _scaleTicks =
    [
        (0.00, "-20"),
        (0.20, "-10"),
        (0.35, "-7"),
        (0.50, "-5"),
        (0.65, "-3"),
        (0.75, null),
        (0.83, "0"),
        (0.90, null),
        (0.95, null),
        (1.00, "+3"),
    ];

    private static readonly Brush _faceBrush = BuildFaceBrush();
    private static readonly Brush _scaleBrush = Freeze(Color.FromRgb(40, 24, 4));
    private static readonly Brush _redZoneBrush = Freeze(Color.FromRgb(190, 40, 30));
    private static readonly Brush _needleBrush = Freeze(Color.FromRgb(20, 15, 10));
    private static readonly Brush _peakDotBrush = Freeze(Color.FromRgb(210, 30, 20));

    private DrawingGroup? _faceCache;
    private Size _faceCacheSize;

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public double PeakLevel
    {
        get => (double)GetValue(PeakLevelProperty);
        set => SetValue(PeakLevelProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var size = new Size(width, height);
        if (_faceCache is null || _faceCacheSize != size)
        {
            _faceCache = BuildFace(width, height);
            _faceCacheSize = size;
        }

        drawingContext.DrawDrawing(_faceCache);

        var pivot = PivotPoint(width, height);
        var radius = Radius(width, height);

        var peakAngle = AngleForPosition(Math.Clamp(PeakLevel, 0, 1));
        var peakPoint = PointOnArc(pivot, radius * 0.9, peakAngle);
        drawingContext.DrawEllipse(_peakDotBrush, null, peakPoint, 3, 3);

        var needleAngle = AngleForPosition(Math.Clamp(Level, 0, 1));
        var needleTip = PointOnArc(pivot, radius, needleAngle);
        drawingContext.DrawLine(new Pen(_needleBrush, 2), pivot, needleTip);
        drawingContext.DrawEllipse(_needleBrush, null, pivot, 4, 4);
    }

    private static DrawingGroup BuildFace(double width, double height)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(_faceBrush, null, new Rect(0, 0, width, height));

            var pivot = PivotPoint(width, height);
            var radius = Radius(width, height);

            DrawArcBand(dc, pivot, radius * 0.95, RedZoneStartPosition, 1.0, new Pen(_redZoneBrush, radius * 0.08));

            var typeface = new Typeface("Segoe UI");
            foreach (var (position, label) in _scaleTicks)
            {
                var angle = AngleForPosition(position);
                var inner = PointOnArc(pivot, radius * 0.78, angle);
                var outer = PointOnArc(pivot, radius * 0.95, angle);
                dc.DrawLine(new Pen(_scaleBrush, 1.5), inner, outer);

                if (label is null)
                {
                    continue;
                }

                var formattedText = new FormattedText(
                    label,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    Math.Max(7, radius * 0.095),
                    _scaleBrush,
                    1.0);

                var textPoint = PointOnArc(pivot, radius * 0.6, angle);
                dc.DrawText(formattedText, new Point(textPoint.X - (formattedText.Width / 2), textPoint.Y - (formattedText.Height / 2)));
            }
        }

        group.Freeze();
        return group;
    }

    private static void DrawArcBand(DrawingContext dc, Point pivot, double radius, double startPosition, double endPosition, Pen pen)
    {
        var startPoint = PointOnArc(pivot, radius, AngleForPosition(startPosition));
        var endPoint = PointOnArc(pivot, radius, AngleForPosition(endPosition));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startPoint, false, false);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static Point PivotPoint(double width, double height) => new(width / 2, height * 0.92);

    private static double Radius(double width, double height) => Math.Min(width / 2, height) * 0.85;

    private static double AngleForPosition(double position) =>
        DegreesToRadians(SweepStartDegrees + (position * (SweepEndDegrees - SweepStartDegrees)));

    private static Point PointOnArc(Point pivot, double radius, double angleRadians) =>
        new(pivot.X + (radius * Math.Sin(angleRadians)), pivot.Y - (radius * Math.Cos(angleRadians)));

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>Warm amber/gold backlit face, like an incandescent bulb glowing behind the dial - not a flat cream color.</summary>
    private static Brush BuildFaceBrush()
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.62),
            Center = new Point(0.5, 0.62),
            RadiusX = 0.75,
            RadiusY = 0.75,
        };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 214, 120), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(232, 175, 68), 0.55));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(184, 122, 32), 0.85));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(140, 88, 22), 1.0));
        brush.Freeze();
        return brush;
    }
}
