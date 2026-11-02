using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>
/// Synthetic Allsky data from a known lens model: the real catalogue at the 20 Oct 2026 02:21:33 AEDT
/// frame time and site, projected into the image, plus junk spots and hot pixels.
/// </summary>
public static class SyntheticSky
{
    public static readonly SiteLocation Site = new(-33.713562, 151.09024, 134);
    public static readonly DateTime FrameUtc = new(2026, 10, 19, 15, 21, 33, DateTimeKind.Utc);

    /// <summary>A lens like the one fitted to Mark's camera (2500 × 2500 frame).</summary>
    public static LensCalibration MarksLens(bool eastClockwise = false) => new()
    {
        CentreX = 1074,
        CentreY = 1226,
        NorthRotationDeg = 354.4,
        EastIsClockwise = eastClockwise,
        RadialCoefficients = new[] { 1019.4, -105.8, 51.7 },
    };

    public static IReadOnlyList<SkyStar> Sky(double limitMag = 4.5) =>
        SkyStars.AboveHorizon(StarCatalogue.LoadEmbedded().BrighterThan(limitMag), Site, FrameUtc);

    /// <summary>
    /// Detected spots: catalogue stars to <paramref name="detectedMag"/> that fall in the frame (with
    /// ±<paramref name="jitterPx"/> position noise), and <paramref name="junk"/> random non-star spots.
    /// Brightest peak first, as the detector returns them.
    /// </summary>
    public static List<DetectedStar> Detected(LensCalibration lens, int size, double detectedMag = 4.0, int junk = 300, double jitterPx = 0.5, int seed = 1)
    {
        var rng = new Random(seed);
        var projection = new LensProjection(lens);
        var spots = new List<DetectedStar>();
        foreach (SkyStar s in Sky(detectedMag))
        {
            if (s.Position.AltDeg < 8) continue; // low stars behind trees
            PixelPoint p = projection.ToPixel(s.Position);
            if (p.X < 0 || p.Y < 0 || p.X >= size || p.Y >= size) continue;
            double peak = (200 * Math.Pow(10, -0.4 * (s.Star.Magnitude + 1.5))) + 10;
            spots.Add(new DetectedStar(new PixelPoint(p.X + Jitter(rng, jitterPx), p.Y + Jitter(rng, jitterPx)), peak * 6, peak, 6));
        }

        for (int i = 0; i < junk; i++)
        {
            double peak = 4 + (rng.NextDouble() * (i < 5 ? 80 : 10)); // a few bright non-stars (lights, gaps), most faint
            spots.Add(new DetectedStar(new PixelPoint(rng.NextDouble() * size, rng.NextDouble() * size), peak * 4, peak, 4));
        }

        spots.Sort((a, b) => b.Peak.CompareTo(a.Peak));
        return spots;
    }

    /// <summary>
    /// An image (scaled down by <paramref name="scale"/>) with Gaussian stars to magnitude 3.5, sky
    /// background with noise, and single hot pixels. Also returns the hot-pixel positions.
    /// </summary>
    public static (GrayFrame Frame, List<PixelPoint> HotPixels) Frame(LensCalibration lens, int size, int seed = 2)
    {
        var rng = new Random(seed);
        var pixels = new float[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = 20f + (float)(Jitter(rng, 1.5));

        var projection = new LensProjection(lens);
        foreach (SkyStar s in Sky(3.5))
        {
            if (s.Position.AltDeg < 8) continue;
            PixelPoint p = projection.ToPixel(s.Position);
            double amp = 220 * Math.Pow(10, -0.4 * (s.Star.Magnitude + 1.5)) + 25;
            Stamp(pixels, size, p.X, p.Y, Math.Min(amp, 230), 1.2);
        }

        var hot = new List<PixelPoint>();
        for (int i = 0; i < 400; i++)
        {
            int x = rng.Next(2, size - 2), y = rng.Next(2, size - 2);
            pixels[(y * size) + x] = 255f;
            hot.Add(new PixelPoint(x, y));
        }

        return (new GrayFrame(size, size, pixels), hot);
    }

    /// <summary>The lens scaled for an image <paramref name="factor"/> times the size.</summary>
    public static LensCalibration Scaled(LensCalibration lens, double factor) => lens with
    {
        CentreX = lens.CentreX * factor,
        CentreY = lens.CentreY * factor,
        RadialCoefficients = lens.RadialCoefficients.Select(c => c * factor).ToArray(),
    };

    public static void Stamp(float[] pixels, int size, double cx, double cy, double amp, double sigma)
    {
        for (int y = (int)cy - 4; y <= (int)cy + 4; y++)
        {
            for (int x = (int)cx - 4; x <= (int)cx + 4; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                double r2 = ((x - cx) * (x - cx)) + ((y - cy) * (y - cy));
                pixels[(y * size) + x] += (float)(amp * Math.Exp(-r2 / (2 * sigma * sigma)));
            }
        }
    }

    private static double Jitter(Random rng, double amount) => ((rng.NextDouble() * 2) - 1) * amount;
}
