using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Astro calculator returning values set by the test.</summary>
public sealed class FakeAstroCalculator : IAstroCalculator
{
    public double SunAltitude { get; set; } = -30.0;

    public double SunAltitudeDeg(SiteLocation site, DateTime utc) => SunAltitude;

    public HorizontalCoordinate MoonPosition(SiteLocation site, DateTime utc) => new(-30.0, 0.0);

    public double MoonIllumination(DateTime utc) => 0.0;

    public HorizontalCoordinate ToHorizontal(double raHours, double decDeg, CoordinateEpoch epoch, SiteLocation site, DateTime utc) =>
        throw new NotSupportedException();

    public (double RaHours, double DecDeg) ToEquatorial(HorizontalCoordinate horizontal, SiteLocation site, DateTime utc) =>
        throw new NotSupportedException();
}
