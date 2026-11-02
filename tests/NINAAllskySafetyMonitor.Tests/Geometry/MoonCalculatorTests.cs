using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

/// <summary>Reference values from astropy 7 (get_body("moon"), no refraction) for the Hornsby site.</summary>
[Requirement("REQ-31")]
public sealed class MoonCalculatorTests
{
    [Theory]
    [InlineData("2026-10-19T15:21:33", 2.50, 247.72)]
    [InlineData("2026-11-05T12:00:00", -51.11, 150.61)]
    [InlineData("2024-01-25T17:54:00", 11.35, 309.60)]
    public void PositionMatchesAstropy(string utc, double alt, double az)
    {
        HorizontalCoordinate moon = MoonCalculator.Position(SyntheticSky.Site, Utc(utc));
        Assert.InRange(SkyGeometry.SeparationDeg(moon, new HorizontalCoordinate(alt, az)), 0, 0.5);
    }

    [Theory]
    [InlineData("2024-01-25T17:54:00", 0.998)]
    [InlineData("2024-01-11T11:57:00", 0.002)]
    [InlineData("2026-10-19T15:21:33", 0.593)]
    [InlineData("2026-11-05T12:00:00", 0.145)]
    public void IlluminationMatchesAstropy(string utc, double expected) =>
        Assert.InRange(MoonCalculator.Illumination(Utc(utc)), expected - 0.02, expected + 0.02);

    private static DateTime Utc(string s) => DateTime.SpecifyKind(DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
}
