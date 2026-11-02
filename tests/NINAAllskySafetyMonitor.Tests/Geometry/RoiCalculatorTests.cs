using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

[Requirement("REQ-06", "REQ-23", "REQ-24", "REQ-26")]
public sealed class RoiCalculatorTests
{
    private static readonly SiteLocation Site = new(-33.713562, 151.09024, 134);
    private static readonly DateTime Utc = new(2026, 11, 2, 11, 0, 0, DateTimeKind.Utc);
    private readonly RoiCalculator calculator = new(new FakeAstroCalculator(), new MountStateInterpreter());

    [Fact]
    public void CentreComesFromRaDecNotTheMountsAltAz()
    {
        // The mount reports Alt 45 / Az 0 (its home-referenced frame); the pole must still come out due south.
        MountState m = FakeMountStateSource.Tracking(0, -90) with { ReportedAltDeg = 45, ReportedAzDeg = 0 };
        (HorizontalCoordinate? centre, string? unusable) = calculator.CalculateCentre(m, Site, Utc);

        Assert.Null(unusable);
        Assert.NotNull(centre);
        Assert.Equal(33.71, centre!.Value.AltDeg, 1);
        Assert.Equal(180.0, centre.Value.AzDeg, 1);
    }

    [Fact]
    public void UnusableMountGivesNoCentreAndTheReason()
    {
        (HorizontalCoordinate? centre, string? unusable) = calculator.CalculateCentre(FakeMountStateSource.Tracking(0, -90) with { AtPark = true }, Site, Utc);
        Assert.Null(centre);
        Assert.Equal("Mount parked", unusable);
    }
}
