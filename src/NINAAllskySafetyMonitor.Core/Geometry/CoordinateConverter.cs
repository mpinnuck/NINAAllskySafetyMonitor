using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// RA/Dec ↔ alt/az for the site and time (sidereal time from the IAU 1982 GMST expression, J2000
/// precessed to the date with the standard annual rates). Accurate to about 0.1°, ample for a 15° ROI.
/// Refraction is ignored.
/// </summary>
[Requirement("REQ-24")]
public static class CoordinateConverter
{
    private const double Rad = Math.PI / 180.0;

    /// <summary>Local sidereal time in degrees.</summary>
    public static double LocalSiderealDeg(DateTime utc, double longitudeDeg)
    {
        double jd = (DateTime.SpecifyKind(utc, DateTimeKind.Utc) - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays + 2451545.0;
        double d = jd - 2451545.0;
        double t = d / 36525.0;
        double gmst = 280.46061837 + (360.98564736629 * d) + (0.000387933 * t * t) - (t * t * t / 38710000.0);
        return SkyGeometry.Normalise(gmst + longitudeDeg);
    }

    /// <summary>Altitude and azimuth (from north through east) of RA (hours) / Dec (degrees).</summary>
    public static HorizontalCoordinate ToHorizontal(double raHours, double decDeg, CoordinateEpoch epoch, SiteLocation site, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(site);
        double ra = raHours * 15.0, dec = decDeg;
        if (epoch == CoordinateEpoch.J2000) (ra, dec) = Precess(ra, dec, YearsSinceJ2000(utc));

        double ha = (LocalSiderealDeg(utc, site.LongitudeDeg) - ra) * Rad;
        double lat = site.LatitudeDeg * Rad, d = dec * Rad;
        double sinAlt = (Math.Sin(d) * Math.Sin(lat)) + (Math.Cos(d) * Math.Cos(lat) * Math.Cos(ha));
        double alt = Math.Asin(Math.Clamp(sinAlt, -1.0, 1.0));
        double az = Math.Atan2(-Math.Cos(d) * Math.Sin(ha), (Math.Sin(d) * Math.Cos(lat)) - (Math.Cos(d) * Math.Sin(lat) * Math.Cos(ha)));
        return new HorizontalCoordinate(alt / Rad, SkyGeometry.Normalise(az / Rad));
    }

    /// <summary>RA (hours) and Dec (degrees) in the given epoch for an alt/az direction.</summary>
    public static (double RaHours, double DecDeg) ToEquatorial(HorizontalCoordinate h, SiteLocation site, DateTime utc, CoordinateEpoch epoch = CoordinateEpoch.JNow)
    {
        ArgumentNullException.ThrowIfNull(site);
        double alt = h.AltDeg * Rad, az = h.AzDeg * Rad, lat = site.LatitudeDeg * Rad;
        double sinDec = (Math.Sin(alt) * Math.Sin(lat)) + (Math.Cos(alt) * Math.Cos(lat) * Math.Cos(az));
        double dec = Math.Asin(Math.Clamp(sinDec, -1.0, 1.0));
        double ha = Math.Atan2(-Math.Sin(az) * Math.Cos(alt), (Math.Sin(alt) * Math.Cos(lat)) - (Math.Cos(alt) * Math.Sin(lat) * Math.Cos(az)));
        double ra = SkyGeometry.Normalise(LocalSiderealDeg(utc, site.LongitudeDeg) - (ha / Rad));
        double decDeg = dec / Rad;
        if (epoch == CoordinateEpoch.J2000) (ra, decDeg) = Precess(ra, decDeg, -YearsSinceJ2000(utc));
        return (ra / 15.0, decDeg);
    }

    private static double YearsSinceJ2000(DateTime utc) =>
        (DateTime.SpecifyKind(utc, DateTimeKind.Utc) - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays / 365.25;

    /// <summary>Precession by the standard annual rates over <paramref name="years"/> (negative to go back).</summary>
    private static (double RaDeg, double DecDeg) Precess(double raDeg, double decDeg, double years)
    {
        double m = 3.07496 * 15.0 / 3600.0, n = 20.0431 / 3600.0; // degrees per year
        double ra = raDeg * Rad, dec = decDeg * Rad;
        double cosDec = Math.Max(1e-6, Math.Abs(Math.Cos(dec)));
        double dRa = (m + (n * Math.Sin(ra) * Math.Sin(dec) / cosDec)) * years;
        double dDec = n * Math.Cos(ra) * years;
        return (SkyGeometry.Normalise(raDeg + dRa), Math.Clamp(decDeg + dDec, -90.0, 90.0));
    }
}
