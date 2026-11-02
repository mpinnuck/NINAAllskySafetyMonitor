using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Position and illuminated fraction of the Moon from the principal terms of the lunar theory (Meeus,
/// Astronomical Algorithms, ch. 47, and the low-precision Sun, ch. 25), with the topocentric parallax
/// applied to the altitude. Accurate to about 0.3°, ample for a 15° exclusion zone.
/// </summary>
[Requirement("REQ-31")]
public static class MoonCalculator
{
    private const double Rad = Math.PI / 180.0;

    /// <summary>Mean horizontal parallax of the Moon, in degrees.</summary>
    private const double ParallaxDeg = 0.9507;

    /// <summary>Topocentric altitude and azimuth of the Moon (refraction ignored).</summary>
    public static HorizontalCoordinate Position(SiteLocation site, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(site);
        double t = Centuries(utc);
        (double lon, double lat) = Ecliptic(t);
        double eps = (23.439291 - (0.0130042 * t)) * Rad;
        double l = lon * Rad, b = lat * Rad;
        double ra = Math.Atan2((Math.Sin(l) * Math.Cos(eps)) - (Math.Tan(b) * Math.Sin(eps)), Math.Cos(l));
        double dec = Math.Asin((Math.Sin(b) * Math.Cos(eps)) + (Math.Cos(b) * Math.Sin(eps) * Math.Sin(l)));

        HorizontalCoordinate geocentric = CoordinateConverter.ToHorizontal(SkyGeometry.Normalise(ra / Rad) / 15.0, dec / Rad, CoordinateEpoch.JNow, site, utc);
        double alt = geocentric.AltDeg - (ParallaxDeg * Math.Cos(geocentric.AltDeg * Rad));
        return new HorizontalCoordinate(alt, geocentric.AzDeg);
    }

    /// <summary>Illuminated fraction, 0 (new) to 1 (full).</summary>
    public static double Illumination(DateTime utc)
    {
        double t = Centuries(utc);
        (double lon, double lat) = Ecliptic(t);
        double m = (357.52911 + (35999.05029 * t)) * Rad;
        double sunLon = 280.46646 + (36000.76983 * t) + (1.914602 * Math.Sin(m)) + (0.019993 * Math.Sin(2 * m));
        double cosElongation = Math.Cos(lat * Rad) * Math.Cos((lon - sunLon) * Rad);
        return Math.Clamp((1 - cosElongation) / 2, 0, 1);
    }

    /// <summary>The Moon's position and illumination together.</summary>
    public static MoonState State(SiteLocation site, DateTime utc) => new(Position(site, utc), Illumination(utc));

    private static double Centuries(DateTime utc) =>
        (DateTime.SpecifyKind(utc, DateTimeKind.Utc) - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays / 36525.0;

    /// <summary>Geocentric ecliptic longitude and latitude of the Moon (degrees, equinox of date).</summary>
    private static (double LonDeg, double LatDeg) Ecliptic(double t)
    {
        double lp = 218.3164477 + (481267.88123421 * t);
        double d = (297.8501921 + (445267.1114034 * t)) * Rad;
        double m = (357.5291092 + (35999.0502909 * t)) * Rad;
        double mp = (134.9633964 + (477198.8675055 * t)) * Rad;
        double f = (93.2720950 + (483202.0175233 * t)) * Rad;

        double lon = lp
            + (6.288774 * Math.Sin(mp))
            + (1.274027 * Math.Sin((2 * d) - mp))
            + (0.658314 * Math.Sin(2 * d))
            + (0.213618 * Math.Sin(2 * mp))
            - (0.185116 * Math.Sin(m))
            - (0.114332 * Math.Sin(2 * f))
            + (0.058793 * Math.Sin((2 * d) - (2 * mp)))
            + (0.057066 * Math.Sin((2 * d) - m - mp))
            + (0.053322 * Math.Sin((2 * d) + mp))
            + (0.045758 * Math.Sin((2 * d) - m))
            - (0.040923 * Math.Sin(m - mp))
            - (0.034720 * Math.Sin(d))
            - (0.030383 * Math.Sin(m + mp));

        double lat = (5.128122 * Math.Sin(f))
            + (0.280602 * Math.Sin(mp + f))
            + (0.277693 * Math.Sin(mp - f))
            + (0.173237 * Math.Sin((2 * d) - f))
            + (0.055413 * Math.Sin((2 * d) - mp + f))
            + (0.046271 * Math.Sin((2 * d) - mp - f));

        return (SkyGeometry.Normalise(lon), lat);
    }
}
