using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

/// <summary>Reference values from astropy 7 (get_sun, no refraction) for the Hornsby site.</summary>
[Requirement("REQ-08", "REQ-42")]
public sealed class SunCalculatorTests
{
    [Theory]
    [InlineData("2026-10-19T15:21:33", -40.02, 146.77)]
    [InlineData("2026-11-02T22:20:00", 41.46, 79.92)]
    [InlineData("2026-06-21T07:00:00", -1.81, 297.21)]
    [InlineData("2026-12-21T09:30:00", -5.21, 237.25)]
    public void PositionMatchesAstropy(string utc, double alt, double az)
    {
        DateTime t = DateTime.SpecifyKind(DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        HorizontalCoordinate sun = SunCalculator.Position(SyntheticSky.Site, t);
        Assert.InRange(SkyGeometry.SeparationDeg(sun, new HorizontalCoordinate(alt, az)), 0, 0.1);
    }

    [Fact]
    public void CoreAstroCalculatorUsesTheCoreCalculators()
    {
        var astro = new CoreAstroCalculator();
        Assert.Equal(SunCalculator.AltitudeDeg(SyntheticSky.Site, SyntheticSky.FrameUtc), astro.SunAltitudeDeg(SyntheticSky.Site, SyntheticSky.FrameUtc));
        Assert.Equal(MoonCalculator.Illumination(SyntheticSky.FrameUtc), astro.MoonIllumination(SyntheticSky.FrameUtc));
    }
}
