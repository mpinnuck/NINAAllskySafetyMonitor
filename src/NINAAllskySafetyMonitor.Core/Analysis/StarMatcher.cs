using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Pairs expected stars with detected stars: each expected star, brightest first, takes the nearest
/// unclaimed detected star within the tolerance. The result has one entry per expected star, null when
/// that star was not found.
/// </summary>
[Requirement("REQ-29")]
public sealed class StarMatcher
{
    public IReadOnlyList<DetectedStar?> Match(IReadOnlyList<ExpectedStar> expected, IReadOnlyList<DetectedStar> detected, double tolerancePx)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(detected);

        var claimed = new bool[detected.Count];
        var found = new DetectedStar?[expected.Count];
        double tol2 = tolerancePx * tolerancePx;
        for (int e = 0; e < expected.Count; e++)
        {
            PixelPoint p = expected[e].Position;
            int best = -1;
            double bestD2 = tol2;
            for (int d = 0; d < detected.Count; d++)
            {
                if (claimed[d]) continue;
                double dx = detected[d].Position.X - p.X, dy = detected[d].Position.Y - p.Y;
                double d2 = (dx * dx) + (dy * dy);
                if (d2 <= bestD2)
                {
                    best = d;
                    bestD2 = d2;
                }
            }

            if (best < 0) continue;
            claimed[best] = true;
            found[e] = detected[best];
        }

        return found;
    }
}
