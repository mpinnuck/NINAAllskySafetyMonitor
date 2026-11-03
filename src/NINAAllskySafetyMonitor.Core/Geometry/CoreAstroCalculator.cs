using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// <see cref="IAstroCalculator"/> from Core alone (Sun, Moon and coordinates), for the offline harness,
/// which has no NINA. The plugin uses NovasAstroCalculator; the two agree to about 0.1°.
/// </summary>
[Requirement("REQ-08", "REQ-24", "REQ-31", "REQ-42")]
public sealed class CoreAstroCalculator : IAstroCalculator
{
    public double SunAltitudeDeg(SiteLocation site, DateTime utc) => SunCalculator.AltitudeDeg(site, utc);

    public HorizontalCoordinate MoonPosition(SiteLocation site, DateTime utc) => MoonCalculator.Position(site, utc);

    public double MoonIllumination(DateTime utc) => MoonCalculator.Illumination(utc);

    public HorizontalCoordinate ToHorizontal(double raHours, double decDeg, CoordinateEpoch epoch, SiteLocation site, DateTime utc) =>
        CoordinateConverter.ToHorizontal(raHours, decDeg, epoch, site, utc);

    public (double RaHours, double DecDeg) ToEquatorial(HorizontalCoordinate horizontal, SiteLocation site, DateTime utc) =>
        CoordinateConverter.ToEquatorial(horizontal, site, utc, CoordinateEpoch.J2000);
}
