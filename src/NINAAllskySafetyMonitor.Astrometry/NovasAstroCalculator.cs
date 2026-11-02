using NINA.Astrometry;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Astrometry;

/// <summary>
/// Implements <see cref="IAstroCalculator"/>. The Sun altitude comes from NINA.Astrometry (NOVAS/SOFA);
/// RA/Dec ↔ alt/az uses Core's <see cref="CoordinateConverter"/> (about 0.1°, ample for a 15° ROI, and
/// testable without NINA). The Moon comes from Core's MoonCalculator for the same reason.
/// </summary>
[Requirement("REQ-08", "REQ-24", "REQ-31")]
public sealed class NovasAstroCalculator : IAstroCalculator
{
    /// <inheritdoc />
    public double SunAltitudeDeg(SiteLocation site, DateTime utc)
    {
        // The latitude/longitude overload exists in every NINA 3.x version; newer versions mark it obsolete.
#pragma warning disable CS0618
        return AstroUtil.GetSunAltitude(DateTime.SpecifyKind(utc, DateTimeKind.Utc), site.LatitudeDeg, site.LongitudeDeg);
#pragma warning restore CS0618
    }

    /// <inheritdoc />
    public HorizontalCoordinate MoonPosition(SiteLocation site, DateTime utc) => MoonCalculator.Position(site, utc);

    /// <inheritdoc />
    public double MoonIllumination(DateTime utc) => MoonCalculator.Illumination(utc);

    /// <inheritdoc />
    public HorizontalCoordinate ToHorizontal(double raHours, double decDeg, CoordinateEpoch epoch, SiteLocation site, DateTime utc) =>
        CoordinateConverter.ToHorizontal(raHours, decDeg, epoch, site, utc);

    /// <inheritdoc />
    public (double RaHours, double DecDeg) ToEquatorial(HorizontalCoordinate horizontal, SiteLocation site, DateTime utc) =>
        CoordinateConverter.ToEquatorial(horizontal, site, utc, CoordinateEpoch.J2000);
}
