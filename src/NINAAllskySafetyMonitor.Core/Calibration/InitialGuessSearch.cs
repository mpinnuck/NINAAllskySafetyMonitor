using System.Numerics;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>
/// Finds a rough lens model to start the fit from. With a simple fisheye (radius ∝ zenith distance) a
/// star's pixel is c + s·k, where c is the zenith pixel, s = scale·e^(i·rotation) and k depends only on
/// the star's alt/az (as complex numbers). So one pair of bright catalogue stars matched to one pair of
/// detected spots fixes c and s. Every pairing of the 30 brightest catalogue stars with the 25 brightest
/// spots, for both east directions, is tried; the model that puts the most bright catalogue stars on a
/// detected spot wins. Falls back to stars the imager identifies (<see cref="FromIdentified"/>).
/// </summary>
[Requirement("REQ-20")]
public sealed class InitialGuessSearch
{
    private const int CatalogueCandidates = 30;
    private const int SpotCandidates = 25;
    private const int ScoringSpots = 150;
    private const double ToleranceFraction = 0.008;
    internal const int MinScore = 15;

    /// <summary>The best starting model, or null when no convincing match is found.</summary>
    public LensCalibration? Search(IReadOnlyList<DetectedStar> detected, int width, int height, IReadOnlyList<SkyStar> sky)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(sky);
        SkyStar[] bright = sky.Where(s => s.Position.AltDeg > 12 && s.Star.Magnitude < 2.6).OrderBy(s => s.Star.Magnitude).Take(CatalogueCandidates).ToArray();
        DetectedStar[] spots = detected.Take(SpotCandidates).ToArray();
        if (bright.Length < 2 || spots.Length < 2) return null;

        double tol = ToleranceFraction * Math.Max(width, height);
        var scoringSet = ScoringStars(sky);
        var index = new SpotIndex(detected.Take(ScoringSpots).ToList(), tol);
        double minScale = 0.25 * Math.Min(width, height) / (Math.PI / 2), maxScale = 1.5 * Math.Max(width, height) / (Math.PI / 2);

        int bestScore = 0;
        (Complex c, Complex s, bool east) best = default;
        foreach (bool east in new[] { false, true })
        {
            Complex[] k = bright.Select(b => K(b.Position, east)).ToArray();
            Complex[] ks = scoringSet.Select(b => K(b.Position, east)).ToArray();
            Complex[] p = spots.Select(d => new Complex(d.Position.X, d.Position.Y)).ToArray();
            for (int i = 0; i < k.Length; i++)
            {
                for (int j = 0; j < k.Length; j++)
                {
                    if (i == j) continue;
                    Complex dk = k[i] - k[j];
                    if (dk.Magnitude < 0.05) continue;
                    for (int a = 0; a < p.Length; a++)
                    {
                        for (int b = 0; b < p.Length; b++)
                        {
                            if (a == b) continue;
                            Complex s = (p[a] - p[b]) / dk;
                            double scale = s.Magnitude;
                            if (scale < minScale || scale > maxScale) continue;
                            Complex c = p[a] - (s * k[i]);
                            if (c.Real < 0.2 * width || c.Real > 0.8 * width || c.Imaginary < 0.2 * height || c.Imaginary > 0.8 * height) continue;
                            int score = Score(c, s, ks, index, tol);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                best = (c, s, east);
                            }
                        }
                    }
                }
            }
        }

        return bestScore >= MinScore ? ToLens(best.c, best.s, best.east) : null;
    }

    /// <summary>
    /// Starting model from two or more identified stars. Both east directions fit two stars exactly, so
    /// the one that puts more catalogue stars on detected spots is chosen.
    /// </summary>
    public LensCalibration? FromIdentified(IReadOnlyList<(SkyStar Star, PixelPoint Position)> picks, IReadOnlyList<DetectedStar> detected, int width, int height, IReadOnlyList<SkyStar> sky)
    {
        ArgumentNullException.ThrowIfNull(picks);
        if (picks.Count < 2) return null;
        double tol = ToleranceFraction * Math.Max(width, height);
        var scoringSet = ScoringStars(sky);
        var index = new SpotIndex(detected.Take(ScoringSpots).ToList(), tol);

        LensCalibration? best = null;
        int bestScore = -1;
        foreach (bool east in new[] { false, true })
        {
            Complex[] k = picks.Select(p => K(p.Star.Position, east)).ToArray();
            Complex[] p = picks.Select(q => new Complex(q.Position.X, q.Position.Y)).ToArray();
            Complex kMean = Mean(k), pMean = Mean(p);
            Complex num = Complex.Zero;
            double den = 0;
            for (int i = 0; i < k.Length; i++)
            {
                num += Complex.Conjugate(k[i] - kMean) * (p[i] - pMean);
                den += (k[i] - kMean).Magnitude * (k[i] - kMean).Magnitude;
            }

            if (den < 1e-12) continue;
            Complex s = num / den;
            Complex c = pMean - (s * kMean);
            int score = Score(c, s, scoringSet.Select(b => K(b.Position, east)).ToArray(), index, tol);
            if (score > bestScore)
            {
                bestScore = score;
                best = ToLens(c, s, east);
            }
        }

        return best;
    }

    /// <summary>k = z·(−i)·e^(±i·az): pixel = c + s·k with s = scale·e^(i·rotation).</summary>
    internal static Complex K(HorizontalCoordinate h, bool eastClockwise)
    {
        double z = (90.0 - h.AltDeg) * Math.PI / 180.0;
        double az = (eastClockwise ? h.AzDeg : -h.AzDeg) * Math.PI / 180.0;
        return z * -Complex.ImaginaryOne * Complex.FromPolarCoordinates(1.0, az);
    }

    internal static LensCalibration ToLens(Complex c, Complex s, bool east) => new()
    {
        CentreX = c.Real,
        CentreY = c.Imaginary,
        NorthRotationDeg = SkyGeometry.Normalise(s.Phase * 180.0 / Math.PI),
        EastIsClockwise = east,
        RadialCoefficients = new[] { s.Magnitude },
    };

    private static List<SkyStar> ScoringStars(IReadOnlyList<SkyStar> sky) =>
        sky.Where(s => s.Position.AltDeg > 5 && s.Star.Magnitude < 3.0).ToList();

    private static int Score(Complex c, Complex s, Complex[] k, SpotIndex index, double tol)
    {
        int score = 0;
        foreach (Complex ki in k)
        {
            Complex q = c + (s * ki);
            if (index.Nearest(q.Real, q.Imaginary, tol) >= 0) score++;
        }

        return score;
    }

    private static Complex Mean(Complex[] v)
    {
        Complex sum = Complex.Zero;
        foreach (Complex x in v) sum += x;
        return sum / v.Length;
    }
}
