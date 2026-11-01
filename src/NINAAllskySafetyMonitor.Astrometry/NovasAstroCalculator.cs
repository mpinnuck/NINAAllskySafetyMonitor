using NINA.Astrometry;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Astrometry;

/// <summary>
/// Implements <see cref="IAstroCalculator"/> over NINA.Astrometry (NOVAS/SOFA).
/// Slice 1 implements the Sun altitude used by the operating window; the Moon and
/// coordinate conversions are added with geometry (slice 5) and cloud detection (slice 6).
/// </summary>
[Requirement("REQ-08")]
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
    public HorizontalCoordinate MoonPosition(SiteLocation site, DateTime utc) =>
        throw new NotSupportedException("Moon position is implemented in slice 6 (cloud detection).");

    /// <inheritdoc />
    public double MoonIllumination(DateTime utc) =>
        throw new NotSupportedException("Moon illumination is implemented in slice 6 (cloud detection).");

    /// <inheritdoc />
    public HorizontalCoordinate ToHorizontal(double raHours, double decDeg, CoordinateEpoch epoch, SiteLocation site, DateTime utc) =>
        throw new NotSupportedException("Coordinate conversion is implemented in slice 5 (geometry).");

    /// <inheritdoc />
    public (double RaHours, double DecDeg) ToEquatorial(HorizontalCoordinate horizontal, SiteLocation site, DateTime utc) =>
        throw new NotSupportedException("Coordinate conversion is implemented in slice 5 (geometry).");
}
