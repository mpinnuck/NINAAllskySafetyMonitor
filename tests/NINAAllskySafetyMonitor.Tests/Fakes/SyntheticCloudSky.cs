using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>
/// 1000 × 1000 synthetic Allsky frames for cloud detection: the real catalogue to magnitude 4.6 at the
/// 20 Oct 2026 frame time, through Mark's lens scaled to the frame, on a background brighter towards the
/// west (like the city glow), with sensor noise. A cloud patch can be laid over a part of the sky: it
/// hides the stars under it (thick) or dims them a little (thin), and raises and roughens the background.
/// </summary>
public static class SyntheticCloudSky
{
    public const int Size = 1000;

    public static LensCalibration Lens { get; } = SyntheticSky.Scaled(SyntheticSky.MarksLens(), Size / 2500.0);

    /// <summary>A direction high in the south, inside the default working area.</summary>
    public static readonly HorizontalCoordinate SouthHigh = new(70, 180);

    /// <param name="exposure">Overall brightness scale, as Allsky's auto-exposure would apply.</param>
    /// <param name="cloudCentre">Centre of the cloud patch, or null for a clear sky.</param>
    /// <param name="cloudRadiusDeg">Radius of the cloud patch on the sky.</param>
    /// <param name="thick">True: stars under the cloud disappear; false: they stay visible.</param>
    /// <param name="cloudBrightness">Background under the cloud × this.</param>
    /// <param name="texture">Relative roughness of the cloud's brightness.</param>
    public static GrayFrame Frame(
        double exposure = 1.0,
        HorizontalCoordinate? cloudCentre = null,
        double cloudRadiusDeg = 20,
        bool thick = true,
        double cloudBrightness = 1.8,
        double texture = 0.25,
        int seed = 3)
    {
        var rng = new Random(seed);
        var projection = new LensProjection(Lens);
        var pixels = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                double background = 20 + (12.0 * x / Size); // brighter towards one side
                if (cloudCentre is HorizontalCoordinate c && UnderCloud(projection, x, y, c, cloudRadiusDeg))
                {
                    background *= cloudBrightness * (1 + (texture * Math.Sin(x / 9.0) * Math.Cos(y / 7.0)));
                }

                pixels[(y * Size) + x] = (float)background;
            }
        }

        foreach (SkyStar s in SyntheticSky.Sky(4.6))
        {
            PixelPoint p = projection.ToPixel(s.Position);
            if (p.X < 5 || p.Y < 5 || p.X >= Size - 5 || p.Y >= Size - 5) continue;
            double amp = (30 * Math.Pow(10, -0.4 * (s.Star.Magnitude - 4.5))) + 6;
            if (cloudCentre is HorizontalCoordinate c && SkyGeometry.SeparationDeg(s.Position, c) < cloudRadiusDeg)
            {
                if (thick) continue;
                amp *= 0.8;
            }

            SyntheticSky.Stamp(pixels, Size, p.X, p.Y, Math.Min(amp, 200), 1.2);
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            double noisy = (pixels[i] + Gaussian(rng, 1.3)) * exposure;
            pixels[i] = (float)Math.Clamp(Math.Round(noisy), 0, 255);
        }

        return new GrayFrame(Size, Size, pixels);
    }

    /// <summary>A drawn notice image: flat grey with a block of "text".</summary>
    public static GrayFrame NoticeImage(int width = Size, int height = Size)
    {
        var pixels = new float[width * height];
        Array.Fill(pixels, 32f);
        for (int y = height / 2; y < (height / 2) + 30; y++)
        {
            for (int x = width / 4; x < width * 3 / 4; x++) pixels[(y * width) + x] = (x / 6) % 2 == 0 ? 230f : 32f;
        }

        return new GrayFrame(width, height, pixels);
    }

    public static RegionOfInterest Roi(HorizontalCoordinate centre, double radiusDeg = 15) => new(centre, radiusDeg, false, 1.0, string.Empty);

    public static RegionOfInterest WholeArea() => new(null, 15, true, 0, "No mount information: judging the whole working area");

    private static bool UnderCloud(LensProjection projection, int x, int y, HorizontalCoordinate centre, double radiusDeg)
    {
        HorizontalCoordinate h = projection.ToHorizontal(new PixelPoint(x, y));
        return h.AltDeg > 0 && SkyGeometry.SeparationDeg(h, centre) < radiusDeg;
    }

    private static double Gaussian(Random rng, double sigma)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
