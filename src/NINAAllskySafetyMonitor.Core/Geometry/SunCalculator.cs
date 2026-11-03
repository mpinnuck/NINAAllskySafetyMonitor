using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Position of the Sun from the low-precision solar coordinates (Meeus, Astronomical Algorithms, ch. 25),
/// accurate to about 0.01° in longitude: ample for the twilight limit. Refraction is ignored.
/// </summary>
[Requirement("REQ-08")]
public static class SunCalculator
{
    private const double Rad = Math.PI / 180.0;

    /// <summary>Topocentric altitude and azimuth of the Sun.</summary>
    public static HorizontalCoordinate Position(SiteLocation site, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(site);
        double t = (DateTime.SpecifyKind(utc, DateTimeKind.Utc) - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays / 36525.0;
        double l0 = 280.46646 + (36000.76983 * t) + (0.0003032 * t * t);
        double m = (357.52911 + (35999.05029 * t) - (0.0001537 * t * t)) * Rad;
        double c = ((1.914602 - (0.004817 * t)) * Math.Sin(m)) + ((0.019993 - (0.000101 * t)) * Math.Sin(2 * m)) + (0.000289 * Math.Sin(3 * m));
        double omega = (125.04 - (1934.136 * t)) * Rad;
        double lambda = (l0 + c - 0.00569 - (0.00478 * Math.Sin(omega))) * Rad;
        double eps = (23.439291 - (0.0130042 * t) + (0.00256 * Math.Cos(omega))) * Rad;
        double ra = Math.Atan2(Math.Cos(eps) * Math.Sin(lambda), Math.Cos(lambda));
        double dec = Math.Asin(Math.Sin(eps) * Math.Sin(lambda));
        return CoordinateConverter.ToHorizontal(SkyGeometry.Normalise(ra / Rad) / 15.0, dec / Rad, CoordinateEpoch.JNow, site, utc);
    }

    /// <summary>Altitude of the Sun in degrees.</summary>
    public static double AltitudeDeg(SiteLocation site, DateTime utc) => Position(site, utc).AltDeg;
}
