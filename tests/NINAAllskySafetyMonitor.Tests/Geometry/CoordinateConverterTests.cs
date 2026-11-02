using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

[Requirement("REQ-24")]
public sealed class CoordinateConverterTests
{
    private static readonly SiteLocation Site = new(-33.713562, 151.09024, 134);
    private static readonly DateTime Utc = new(2026, 11, 2, 11, 0, 0, DateTimeKind.Utc);
    private static double Lst => CoordinateConverter.LocalSiderealDeg(Utc, Site.LongitudeDeg);

    [Fact]
    public void GreenwichSiderealTimeMatchesReference()
    {
        // Reference value from astropy (mean sidereal time, Greenwich) for 2026-11-02 11:00 UTC: 206.7348°.
        Assert.InRange(CoordinateConverter.LocalSiderealDeg(Utc, 0), 206.7248, 206.7448);
    }

    [Fact]
    public void SouthCelestialPoleIsDueSouthAtTheLatitude()
    {
        HorizontalCoordinate h = CoordinateConverter.ToHorizontal(0, -90, CoordinateEpoch.JNow, Site, Utc);
        Assert.Equal(33.7136, h.AltDeg, 3);
        Assert.Equal(180.0, h.AzDeg, 3);
    }

    [Fact]
    public void DecEqualToLatitudeOnTheMeridianIsTheZenith()
    {
        HorizontalCoordinate h = CoordinateConverter.ToHorizontal(Lst / 15.0, Site.LatitudeDeg, CoordinateEpoch.JNow, Site, Utc);
        Assert.Equal(90.0, h.AltDeg, 3);
    }

    [Fact]
    public void EquatorOnTheMeridianIsNorthFromTheSouthernHemisphere()
    {
        HorizontalCoordinate h = CoordinateConverter.ToHorizontal(Lst / 15.0, 0, CoordinateEpoch.JNow, Site, Utc);
        Assert.Equal(90.0 + Site.LatitudeDeg, h.AltDeg, 3);
        Assert.Equal(0.0, SkyGeometry.SeparationDeg(h, new HorizontalCoordinate(h.AltDeg, 0)), 3); // due north (az 0 or 359.99…)
    }

    [Fact]
    public void StarEastOfTheMeridianHasEasterlyAzimuth()
    {
        HorizontalCoordinate h = CoordinateConverter.ToHorizontal((Lst + 30) / 15.0, -30, CoordinateEpoch.JNow, Site, Utc);
        Assert.InRange(h.AzDeg, 80, 100);
        Assert.InRange(h.AltDeg, 60, 70);
    }

    [Theory]
    [InlineData(5.5867, -5.4, 6.33, 92.276)]     // Orion nebula, about 92° (east), low
    [InlineData(0.7133, -59.9, 62.48, 165.991)]  // high in the south
    [InlineData(20.0, -50.0, 45.228, 230.574)]   // south-west
    public void MatchesReferenceWithinATenthOfADegree(double raHours, double decDeg, double alt, double az)
    {
        // Reference: astropy, mean equator of date, no refraction.
        HorizontalCoordinate h = CoordinateConverter.ToHorizontal(raHours, decDeg, CoordinateEpoch.JNow, Site, Utc);
        Assert.InRange(h.AltDeg, alt - 0.1, alt + 0.1);
        Assert.InRange(h.AzDeg, az - 0.1, az + 0.1);
    }

    [Fact]
    public void J2000IsPrecessedToTheDate()
    {
        HorizontalCoordinate jnow = CoordinateConverter.ToHorizontal(6.0, -30, CoordinateEpoch.JNow, Site, Utc);
        HorizontalCoordinate j2000 = CoordinateConverter.ToHorizontal(6.0, -30, CoordinateEpoch.J2000, Site, Utc);
        double moved = SkyGeometry.SeparationDeg(jnow, j2000);
        Assert.InRange(moved, 0.1, 0.6); // about 27 years of precession (0.2° here)
    }

    [Theory]
    [InlineData(CoordinateEpoch.JNow)]
    [InlineData(CoordinateEpoch.J2000)]
    public void ToEquatorialReversesToHorizontal(CoordinateEpoch epoch)
    {
        HorizontalCoordinate h = CoordinateConverter.ToHorizontal(3.25, -42.0, epoch, Site, Utc);
        (double ra, double dec) = CoordinateConverter.ToEquatorial(h, Site, Utc, epoch);
        Assert.Equal(3.25, ra, 2);
        Assert.Equal(-42.0, dec, 2);
    }
}
