using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Astro calculator returning values set by the test. Coordinates use Core's converter unless a fixed position is set.</summary>
public sealed class FakeAstroCalculator : IAstroCalculator
{
    public double SunAltitude { get; set; } = -30.0;

    /// <summary>When set, ToHorizontal returns this instead of converting.</summary>
    public HorizontalCoordinate? FixedHorizontal { get; set; }

    public double SunAltitudeDeg(SiteLocation site, DateTime utc) => SunAltitude;

    public HorizontalCoordinate MoonPosition(SiteLocation site, DateTime utc) => new(-30.0, 0.0);

    public double MoonIllumination(DateTime utc) => 0.0;

    public HorizontalCoordinate ToHorizontal(double raHours, double decDeg, CoordinateEpoch epoch, SiteLocation site, DateTime utc) =>
        FixedHorizontal ?? CoordinateConverter.ToHorizontal(raHours, decDeg, epoch, site, utc);

    public (double RaHours, double DecDeg) ToEquatorial(HorizontalCoordinate horizontal, SiteLocation site, DateTime utc) =>
        CoordinateConverter.ToEquatorial(horizontal, site, utc, CoordinateEpoch.J2000);
}
