using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>Positions of catalogue stars on the sky for a site and time.</summary>
[Requirement("REQ-20", "REQ-24")]
public static class SkyStars
{
    /// <summary>The stars above <paramref name="minAltDeg"/>, brightest first.</summary>
    public static IReadOnlyList<SkyStar> AboveHorizon(IEnumerable<CatalogueStar> stars, SiteLocation site, DateTime utc, double minAltDeg = 0)
    {
        ArgumentNullException.ThrowIfNull(stars);
        ArgumentNullException.ThrowIfNull(site);
        return stars
            .Select(s => new SkyStar(s, CoordinateConverter.ToHorizontal(s.RaHours, s.DecDeg, CoordinateEpoch.J2000, site, utc)))
            .Where(s => s.Position.AltDeg > minAltDeg)
            .OrderBy(s => s.Star.Magnitude)
            .ToList();
    }
}
