using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>
/// Fits the lens model to catalogue stars. Starting from a rough model, each catalogue star is matched to
/// the nearest unclaimed detected star within a search distance that narrows stage by stage (60 → 6 px
/// for a 2500 px frame), and the zenith position, north rotation and radial terms c1..c3 are refined by
/// least squares. Matches more than 3× the median error are dropped at each stage. Brighter stars claim
/// spots first.
/// </summary>
[Requirement("REQ-19", "REQ-20")]
public sealed class LensFitter
{
    internal const int MinMatches = 6;
    private static readonly (double Radius, int FreeTerms)[] Stages =
    {
        (60, 1), (40, 2), (25, 3), (15, 3), (10, 3), (8, 3), (6, 3),
    };

    /// <summary>The fitted model, or null when too few stars match.</summary>
    public LensFitResult? Fit(IReadOnlyList<DetectedStar> detected, LensCalibration start, IReadOnlyList<SkyStar> sky, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(sky);
        SkyStar[] stars = sky.Where(s => s.Position.AltDeg > 3).OrderBy(s => s.Star.Magnitude).ToArray();
        double unit = Math.Max(width, height) / 2500.0;
        var index = new SpotIndex(detected, 64 * unit);
        bool east = start.EastIsClockwise;
        double[] p = Parameters(start);
        List<(SkyStar Star, PixelPoint Spot)> pairs = new();

        foreach ((double radius, int terms) in Stages)
        {
            pairs = Match(p, east, stars, detected, index, radius * unit);
            if (pairs.Count < MinMatches) return null;
            bool[] free = { true, true, true, true, terms >= 2, terms >= 3 };
            p = LevenbergMarquardt.Minimise(q => Residuals(q, east, pairs), p, free);

            double[] err = Errors(p, east, pairs);
            double limit = Math.Max(3 * Median(err), 2 * unit);
            var kept = pairs.Where((_, i) => err[i] <= limit).ToList();
            if (kept.Count < pairs.Count && kept.Count >= MinMatches)
            {
                pairs = kept;
                p = LevenbergMarquardt.Minimise(q => Residuals(q, east, pairs), p, free);
            }
        }

        if (p[3] <= 0) return null;
        LensCalibration lens = ToLens(p, east, start);
        var projection = new LensProjection(lens);
        var matches = pairs.Select(m =>
        {
            PixelPoint predicted = projection.ToPixel(m.Star.Position);
            double px = Distance(predicted, m.Spot);
            double deg = SkyGeometry.SeparationDeg(projection.ToHorizontal(m.Spot), m.Star.Position);
            return new StarMatch(m.Star, m.Spot, predicted, px, deg);
        }).ToList();

        double rmsPx = Math.Sqrt(matches.Average(m => m.ErrorPx * m.ErrorPx));
        double rmsDeg = Math.Sqrt(matches.Average(m => m.ErrorDeg * m.ErrorDeg));
        return new LensFitResult(lens with { StarsMatched = matches.Count, RmsErrorPx = rmsPx, RmsErrorDeg = rmsDeg }, matches, rmsPx, rmsDeg);
    }

    private static List<(SkyStar, PixelPoint)> Match(double[] p, bool east, SkyStar[] stars, IReadOnlyList<DetectedStar> detected, SpotIndex index, double radius)
    {
        var claimed = new HashSet<int>();
        var pairs = new List<(SkyStar, PixelPoint)>();
        foreach (SkyStar s in stars)
        {
            (double x, double y) = Project(p, east, s.Position);
            int i = index.Nearest(x, y, radius);
            if (i < 0 || !claimed.Add(i)) continue;
            pairs.Add((s, detected[i].Position));
        }

        return pairs;
    }

    private static double[] Residuals(double[] p, bool east, List<(SkyStar Star, PixelPoint Spot)> pairs)
    {
        var r = new double[pairs.Count * 2];
        for (int i = 0; i < pairs.Count; i++)
        {
            (double x, double y) = Project(p, east, pairs[i].Star.Position);
            r[2 * i] = x - pairs[i].Spot.X;
            r[(2 * i) + 1] = y - pairs[i].Spot.Y;
        }

        return r;
    }

    private static double[] Errors(double[] p, bool east, List<(SkyStar Star, PixelPoint Spot)> pairs) =>
        pairs.Select(m =>
        {
            (double x, double y) = Project(p, east, m.Star.Position);
            return Math.Sqrt(((x - m.Spot.X) * (x - m.Spot.X)) + ((y - m.Spot.Y) * (y - m.Spot.Y)));
        }).ToArray();

    /// <summary>Same mapping as <see cref="LensProjection.ToPixel"/>, on the parameter vector [cx, cy, rot, c1, c2, c3].</summary>
    internal static (double X, double Y) Project(double[] p, bool east, HorizontalCoordinate h)
    {
        double z = (90.0 - h.AltDeg) * Math.PI / 180.0;
        double r = (p[3] * z) + (p[4] * z * z) + (p[5] * z * z * z);
        double theta = (p[2] + (east ? h.AzDeg : -h.AzDeg)) * Math.PI / 180.0;
        return (p[0] + (r * Math.Sin(theta)), p[1] - (r * Math.Cos(theta)));
    }

    private static double[] Parameters(LensCalibration l)
    {
        double[] c = l.RadialCoefficients;
        return new[] { l.CentreX, l.CentreY, l.NorthRotationDeg, c.Length > 0 ? c[0] : 1000, c.Length > 1 ? c[1] : 0, c.Length > 2 ? c[2] : 0 };
    }

    private static LensCalibration ToLens(double[] p, bool east, LensCalibration start) => start with
    {
        CentreX = p[0],
        CentreY = p[1],
        NorthRotationDeg = SkyGeometry.Normalise(p[2]),
        EastIsClockwise = east,
        RadialCoefficients = new[] { p[3], p[4], p[5] },
        Manual = false,
    };

    private static double Distance(PixelPoint a, PixelPoint b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static double Median(double[] v)
    {
        if (v.Length == 0) return 0;
        var s = (double[])v.Clone();
        Array.Sort(s);
        return s[s.Length / 2];
    }
}
