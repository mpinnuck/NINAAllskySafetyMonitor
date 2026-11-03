using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Catalogue stars, down to the limiting magnitude, that should be visible in the ROI: their image
/// position falls inside the ROI mask (the ROI circle within the working area, so stars behind trees are
/// never expected), and, while the Moon is up, they are more than the exclusion distance from it.
/// Brightest first.
/// </summary>
[Requirement("REQ-29", "REQ-31")]
public sealed class ExpectedStarPredictor
{
    private readonly StarCatalogue catalogue;
    private readonly object sync = new();
    private (SiteLocation Site, DateTime Utc, double LimitMag, IReadOnlyList<(CatalogueStar Star, HorizontalCoordinate Sky)> Stars)? lastSky;

    public ExpectedStarPredictor(StarCatalogue catalogue)
    {
        this.catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
    }

    public IReadOnlyList<ExpectedStar> Predict(
        PixelMask roiMask,
        LensProjection projection,
        SiteLocation site,
        DateTime utc,
        double limitMag,
        MoonState? moon,
        double moonExclusionDeg)
    {
        ArgumentNullException.ThrowIfNull(roiMask);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(site);

        var expected = new List<ExpectedStar>();
        foreach ((CatalogueStar star, HorizontalCoordinate sky) in StarsAbove(site, utc, limitMag))
        {
            if (moon is { IsUp: true } m && SkyGeometry.SeparationDeg(sky, m.Position) < moonExclusionDeg) continue;
            PixelPoint p = projection.ToPixel(sky);
            if (!roiMask.Contains((int)Math.Round(p.X), (int)Math.Round(p.Y))) continue;
            expected.Add(new ExpectedStar(star, sky, p));
        }

        expected.Sort((a, b) => a.Star.Magnitude.CompareTo(b.Star.Magnitude));
        return expected;
    }

    /// <summary>Catalogue stars above the horizon, worked out once per frame time however many ROIs are judged.</summary>
    private IReadOnlyList<(CatalogueStar Star, HorizontalCoordinate Sky)> StarsAbove(SiteLocation site, DateTime utc, double limitMag)
    {
        lock (sync)
        {
            if (lastSky is { } c && c.Utc == utc && c.LimitMag == limitMag && c.Site == site) return c.Stars;
        }

        var stars = new List<(CatalogueStar, HorizontalCoordinate)>();
        foreach (CatalogueStar star in catalogue.BrighterThan(limitMag))
        {
            HorizontalCoordinate sky = CoordinateConverter.ToHorizontal(star.RaHours, star.DecDeg, CoordinateEpoch.J2000, site, utc);
            if (sky.AltDeg > 0) stars.Add((star, sky));
        }

        lock (sync) lastSky = (site, utc, limitMag, stars);
        return stars;
    }
}
