namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>Spherical geometry on the alt/az sky.</summary>
public static class SkyGeometry
{
    private const double Rad = Math.PI / 180.0;

    /// <summary>Angle between two directions on the sky, in degrees.</summary>
    public static double SeparationDeg(Domain.HorizontalCoordinate a, Domain.HorizontalCoordinate b)
    {
        double a1 = a.AltDeg * Rad, a2 = b.AltDeg * Rad, dz = (b.AzDeg - a.AzDeg) * Rad;
        double s = Math.Sin((a2 - a1) / 2);
        double t = Math.Sin(dz / 2);
        double h = (s * s) + (Math.Cos(a1) * Math.Cos(a2) * t * t);
        return 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(h))) / Rad;
    }

    /// <summary>
    /// The point <paramref name="distanceDeg"/> away from <paramref name="start"/> in direction <paramref name="bearingDeg"/>
    /// (0 = towards the zenith, 90 = towards increasing azimuth).
    /// </summary>
    public static Domain.HorizontalCoordinate Destination(Domain.HorizontalCoordinate start, double bearingDeg, double distanceDeg)
    {
        if (start.AltDeg > 90.0 - 1e-9)
        {
            // At the zenith every direction is "away from the zenith", so the bearing is taken relative to
            // the start azimuth (as if just below the zenith on that azimuth): 0 goes over to the opposite
            // azimuth, 90 towards increasing azimuth.
            return new Domain.HorizontalCoordinate(90.0 - distanceDeg, Normalise(start.AzDeg + 180.0 - bearingDeg));
        }

        double lat1 = start.AltDeg * Rad, lon1 = start.AzDeg * Rad, brg = bearingDeg * Rad, d = distanceDeg * Rad;
        double lat2 = Math.Asin((Math.Sin(lat1) * Math.Cos(d)) + (Math.Cos(lat1) * Math.Sin(d) * Math.Cos(brg)));
        double lon2 = lon1 + Math.Atan2(Math.Sin(brg) * Math.Sin(d) * Math.Cos(lat1), Math.Cos(d) - (Math.Sin(lat1) * Math.Sin(lat2)));
        return new Domain.HorizontalCoordinate(lat2 / Rad, Normalise(lon2 / Rad));
    }

    /// <summary>Angle in [0, 360).</summary>
    public static double Normalise(double deg)
    {
        double r = deg % 360.0;
        return r < 0 ? r + 360.0 : r;
    }
}
