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
/// Draws the overlay on an Allsky image: working area (and its outline), ROI, compass points, and for
/// calibration the alt/az grid, catalogue stars and identified stars. Sky positions are placed with the
/// lens model. Line widths scale with the image, so the same drawing works for the panel and the viewer.
/// </summary>
[Requirement("REQ-21", "REQ-22", "REQ-23", "REQ-45")]
public static class OverlayImageComposer
{
    private const double StepDeg = 2.0;
    private static readonly Color Yellow = Color.FromRgb(0xFF, 0xD5, 0x4F);
    private static readonly Color Green = Color.FromRgb(0x4C, 0xE0, 0x6A);
    private static readonly Color Blue = Color.FromRgb(0x4F, 0xC3, 0xF7);
    private static readonly Color Orange = Color.FromRgb(0xFF, 0x8A, 0x3D);
    private static readonly Color Grey = Color.FromRgb(0x9E, 0xA4, 0xB0);
    private static readonly Color Cyan = Color.FromRgb(0x5E, 0xC8, 0xE5);
    private static readonly Color Magenta = Color.FromRgb(0xFF, 0x5C, 0xC8);

    /// <summary>Panel drawing: working area, ROI and compass.</summary>
    public static BitmapSource Compose(BitmapSource image, int originalWidth, LensCalibration lens, WorkingAreaDefinition area, RegionOfInterest? roi) =>
        Compose(image, originalWidth, lens, new OverlayContent { WorkingArea = area, Roi = roi, Compass = true });

    /// <summary>The image with <paramref name="content"/> drawn on it.</summary>
    /// <param name="image">The image as decoded (may be smaller than the original).</param>
    /// <param name="originalWidth">Width of the original Allsky image (the lens model's pixels).</param>
    /// <param name="lens">Lens model for the original image.</param>
    /// <param name="content">What to draw.</param>
    public static BitmapSource Compose(BitmapSource image, int originalWidth, LensCalibration lens, OverlayContent content)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(lens);
        ArgumentNullException.ThrowIfNull(content);

        int w = image.PixelWidth, h = image.PixelHeight;
        double scale = originalWidth > 0 ? (double)w / originalWidth : 1.0;
        double k = Math.Max(1.0, w / 800.0);
        var projection = new LensProjection(lens);
        Point Px(PixelPoint p) => new(p.X * scale, p.Y * scale);
        Point Sky(HorizontalCoordinate c) => Px(projection.ToPixel(c));

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawImage(image, new Rect(0, 0, w, h));

            if (content.Grid)
            {
                Pen grid = MakePen(Cyan, 1 * k, null);
                foreach (double alt in new[] { 0.0, 30.0, 60.0 })
                {
                    dc.DrawGeometry(null, grid, Polyline(Enumerable.Range(0, 181).Select(i => Sky(new HorizontalCoordinate(alt, i * StepDeg))), closed: true));
                }

                for (int az = 0; az < 360; az += 45)
                {
                    dc.DrawGeometry(null, grid, Polyline(Enumerable.Range(0, 19).Select(i => Sky(new HorizontalCoordinate(i * 5.0, az))), closed: false));
                }
            }

            bool wholeArea = content.Roi?.UsesWholeWorkingArea == true;
            if (content.WorkingArea is { } area)
            {
                Pen areaPen = wholeArea ? MakePen(Green, 3 * k, null) : MakePen(Yellow, 2 * k, DashStyles.Dash);
                dc.DrawGeometry(null, areaPen, Circle(area.Centre, area.RadiusDeg, Sky));
                if (area.Outline.Count >= 3)
                {
                    dc.DrawGeometry(null, MakePen(Blue, 2 * k, null), Polyline(area.Outline.Select(Px), closed: true));
                }
            }

            if (content.DraftOutline.Count > 0)
            {
                Pen draft = MakePen(Blue, 2 * k, DashStyles.Dash);
                dc.DrawGeometry(null, draft, Polyline(content.DraftOutline.Select(Px), closed: content.DraftOutline.Count >= 3));
                foreach (PixelPoint p in content.DraftOutline) dc.DrawEllipse(new SolidColorBrush(Blue), null, Px(p), 3 * k, 3 * k);
            }

            if (content.Roi?.Centre is HorizontalCoordinate centre)
            {
                Pen pen = wholeArea ? MakePen(Orange, 2 * k, DashStyles.Dot) : MakePen(Green, 3 * k, null);
                dc.DrawGeometry(null, pen, Circle(centre, content.Roi.RadiusDeg, Sky));
                Point c = Sky(centre);
                dc.DrawLine(pen, new Point(c.X - (6 * k), c.Y), new Point(c.X + (6 * k), c.Y));
                dc.DrawLine(pen, new Point(c.X, c.Y - (6 * k)), new Point(c.X, c.Y + (6 * k)));
            }

            foreach (OverlayStar s in content.Stars)
            {
                Color colour = s.Matched ? Green : Grey;
                Point p = Px(s.Position);
                dc.DrawEllipse(null, MakePen(colour, 1.5 * k, null), p, 7 * k, 7 * k);
                if (s.Label is { Length: > 0 }) DrawLabel(dc, s.Label, new Point(p.X + (9 * k), p.Y - (8 * k)), 11 * k, colour);
            }

            foreach ((PixelPoint position, string label) in content.Picks)
            {
                Point p = Px(position);
                Pen pick = MakePen(Magenta, 2 * k, null);
                dc.DrawLine(pick, new Point(p.X - (10 * k), p.Y), new Point(p.X + (10 * k), p.Y));
                dc.DrawLine(pick, new Point(p.X, p.Y - (10 * k)), new Point(p.X, p.Y + (10 * k)));
                DrawLabel(dc, label, new Point(p.X + (12 * k), p.Y + (2 * k)), 12 * k, Magenta);
            }

            if (content.Compass)
            {
                foreach ((string label, double az) in new[] { ("N", 0.0), ("E", 90.0), ("S", 180.0), ("W", 270.0) })
                {
                    Point p = Sky(new HorizontalCoordinate(4, az));
                    DrawLabel(dc, label, p, Math.Max(12, w / 45.0), Colors.White, centred: true);
                }
            }
        }

        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawLabel(DrawingContext dc, string text, Point at, double size, Color colour, bool centred = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, new SolidColorBrush(colour), 1.0);
        dc.DrawText(ft, centred ? new Point(at.X - (ft.Width / 2), at.Y - (ft.Height / 2)) : at);
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

        return Polyline(points, closed: true);
    }

    private static WpfGeometry Polyline(IEnumerable<Point> points, bool closed)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            bool first = true;
            foreach (Point p in points)
            {
                if (first)
                {
                    ctx.BeginFigure(p, isFilled: false, isClosed: closed);
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

    private static Pen MakePen(Color colour, double thickness, DashStyle? dash)
    {
        var pen = new Pen(new SolidColorBrush(colour), thickness);
        if (dash is not null) pen.DashStyle = dash;
        pen.Freeze();
        return pen;
    }
}
