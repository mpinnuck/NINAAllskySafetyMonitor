using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using WpfGeometry = System.Windows.Media.Geometry;

namespace NINAAllskySafetyMonitor.Plugin.Imaging;

/// <summary>
/// Draws the working area, its drawn outline, the ROI and compass points over the Allsky image for the
/// status panel. Sky positions are placed with the lens model, so until the lens is calibrated (slice 5b)
/// the drawing is approximate; the ROI itself is computed on the sky and does not depend on it.
/// </summary>
[Requirement("REQ-22", "REQ-23", "REQ-45")]
public static class OverlayImageComposer
{
    private const double StepDeg = 2.0;
    private static readonly Pen AreaPen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F)), 2) { DashStyle = DashStyles.Dash });
    private static readonly Pen AreaJudgedPen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x4C, 0xE0, 0x6A)), 3));
    private static readonly Pen OutlinePen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)), 2));
    private static readonly Pen RoiPen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x4C, 0xE0, 0x6A)), 3));
    private static readonly Pen RoiUnusedPen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x3D)), 2) { DashStyle = DashStyles.Dot });
    private static readonly Brush LabelBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)));

    /// <summary>
    /// The image with the overlay drawn on it.
    /// </summary>
    /// <param name="image">The image as decoded for the panel (may be smaller than the original).</param>
    /// <param name="originalWidth">Width of the original Allsky image, in pixels (the lens model's scale).</param>
    /// <param name="lens">Lens model for the original image.</param>
    /// <param name="area">The working area.</param>
    /// <param name="roi">This cycle's ROI, or null when none was placed (idle).</param>
    public static BitmapSource Compose(BitmapSource image, int originalWidth, LensCalibration lens, WorkingAreaDefinition area, RegionOfInterest? roi)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(lens);
        ArgumentNullException.ThrowIfNull(area);

        int w = image.PixelWidth, h = image.PixelHeight;
        double scale = originalWidth > 0 ? (double)w / originalWidth : 1.0;
        var projection = new LensProjection(lens);
        Point ToPoint(HorizontalCoordinate c)
        {
            PixelPoint p = projection.ToPixel(c);
            return new Point(p.X * scale, p.Y * scale);
        }

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawImage(image, new Rect(0, 0, w, h));

            bool wholeArea = roi?.UsesWholeWorkingArea == true;
            dc.DrawGeometry(null, wholeArea ? AreaJudgedPen : AreaPen, Circle(area.Centre, area.RadiusDeg, ToPoint));

            if (area.Outline.Count >= 3)
            {
                dc.DrawGeometry(null, OutlinePen, Polygon(area.Outline.Select(p => new Point(p.X * scale, p.Y * scale))));
            }

            if (roi?.Centre is HorizontalCoordinate centre)
            {
                Pen pen = wholeArea ? RoiUnusedPen : RoiPen;
                dc.DrawGeometry(null, pen, Circle(centre, roi.RadiusDeg, ToPoint));
                Point c = ToPoint(centre);
                dc.DrawLine(pen, new Point(c.X - 6, c.Y), new Point(c.X + 6, c.Y));
                dc.DrawLine(pen, new Point(c.X, c.Y - 6), new Point(c.X, c.Y + 6));
            }

            double fontSize = Math.Max(12, w / 45.0);
            foreach ((string label, double az) in new[] { ("N", 0.0), ("E", 90.0), ("S", 180.0), ("W", 270.0) })
            {
                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontSize, LabelBrush, 1.0);
                Point p = ToPoint(new HorizontalCoordinate(4, az));
                dc.DrawText(text, new Point(p.X - (text.Width / 2), p.Y - (text.Height / 2)));
            }
        }

        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>A circle on the sky as a closed line; any part below the horizon follows the horizon.</summary>
    private static WpfGeometry Circle(HorizontalCoordinate centre, double radiusDeg, Func<HorizontalCoordinate, Point> toPoint)
    {
        var points = new List<Point>();
        for (double b = 0; b < 360; b += StepDeg)
        {
            HorizontalCoordinate p = SkyGeometry.Destination(centre, b, radiusDeg);
            points.Add(toPoint(p.AltDeg < 0 ? p with { AltDeg = 0 } : p));
        }

        return Polygon(points);
    }

    private static WpfGeometry Polygon(IEnumerable<Point> points)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            bool first = true;
            foreach (Point p in points)
            {
                if (first)
                {
                    ctx.BeginFigure(p, isFilled: false, isClosed: true);
                    first = false;
                }
                else
                {
                    ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
