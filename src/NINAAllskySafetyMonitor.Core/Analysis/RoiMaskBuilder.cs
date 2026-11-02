using System.Globalization;
using System.Text;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Image pixels to analyse. The working-area mask holds the pixels inside the working area (the circle on
/// the sky, cut by the drawn outline); it changes only when the lens, the working area or the image size
/// changes, so the last one is kept. The ROI mask holds the pixels inside both the ROI circle and the
/// working area, or the whole working area when the ROI uses it. Pixels are tested in
/// <see cref="Step"/> × <see cref="Step"/> cells, which is ample for 32 px statistics and star positions.
/// </summary>
[Requirement("REQ-25", "REQ-29")]
public sealed class RoiMaskBuilder
{
    /// <summary>Cell size in pixels.</summary>
    public const int Step = 4;

    private readonly object sync = new();
    private string? areaKey;
    private PixelMask? areaMask;

    /// <summary>Pixels inside the working area.</summary>
    public PixelMask BuildArea(WorkingArea area, LensProjection projection, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(projection);
        string key = Key(area.Definition, projection.Calibration, width, height);
        lock (sync)
        {
            if (areaMask is not null && areaKey == key) return areaMask;
        }

        WorkingAreaDefinition def = area.Definition;
        bool outline = def.Outline.Count >= 3;
        var mask = new PixelMask(width, height);
        for (int y = 0; y < height; y += Step)
        {
            for (int x = 0; x < width; x += Step)
            {
                var p = new PixelPoint(x + (Step / 2.0), y + (Step / 2.0));
                HorizontalCoordinate h = projection.ToHorizontal(p);
                if (h.AltDeg <= 0 || SkyGeometry.SeparationDeg(h, def.Centre) > def.RadiusDeg) continue;
                if (outline && !WorkingArea.InsidePolygon(p, def.Outline)) continue;
                Fill(mask, x, y);
            }
        }

        lock (sync)
        {
            areaKey = key;
            areaMask = mask;
        }

        return mask;
    }

    /// <summary>Pixels inside the ROI circle and the working area (or the whole working area).</summary>
    public PixelMask Build(RegionOfInterest roi, WorkingArea area, LensProjection projection, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(roi);
        PixelMask whole = BuildArea(area, projection, width, height);
        if (roi.UsesWholeWorkingArea || roi.Centre is not HorizontalCoordinate centre) return whole;

        // Bounding box of the ROI circle in the image.
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (int bearing = 0; bearing < 360; bearing += 10)
        {
            PixelPoint p = projection.ToPixel(SkyGeometry.Destination(centre, bearing, roi.RadiusDeg));
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        }

        int x0 = Math.Max(0, ((int)minX / Step * Step) - Step), y0 = Math.Max(0, ((int)minY / Step * Step) - Step);
        int x1 = Math.Min(width, (int)maxX + (2 * Step)), y1 = Math.Min(height, (int)maxY + (2 * Step));
        var mask = new PixelMask(width, height);
        for (int y = y0; y < y1; y += Step)
        {
            for (int x = x0; x < x1; x += Step)
            {
                int cx = Math.Min(x + (Step / 2), width - 1), cy = Math.Min(y + (Step / 2), height - 1);
                if (!whole.Contains(cx, cy)) continue;
                if (SkyGeometry.SeparationDeg(projection.ToHorizontal(new PixelPoint(cx, cy)), centre) > roi.RadiusDeg) continue;
                Fill(mask, x, y);
            }
        }

        return mask;
    }

    private static void Fill(PixelMask mask, int x, int y)
    {
        for (int dy = 0; dy < Step; dy++)
        {
            for (int dx = 0; dx < Step; dx++) mask.Add(x + dx, y + dy);
        }
    }

    private static string Key(WorkingAreaDefinition def, LensCalibration lens, int width, int height)
    {
        var k = new StringBuilder();
        k.Append(CultureInfo.InvariantCulture, $"{width}x{height}|{lens.CentreX:R},{lens.CentreY:R},{lens.NorthRotationDeg:R},{lens.EastIsClockwise}");
        foreach (double c in lens.RadialCoefficients) k.Append(CultureInfo.InvariantCulture, $",{c:R}");
        k.Append(CultureInfo.InvariantCulture, $"|{def.CentreAltDeg:R},{def.CentreAzDeg:R},{def.RadiusDeg:R}");
        foreach (PixelPoint p in def.Outline) k.Append(CultureInfo.InvariantCulture, $";{p.X:R},{p.Y:R}");
        return k.ToString();
    }
}
