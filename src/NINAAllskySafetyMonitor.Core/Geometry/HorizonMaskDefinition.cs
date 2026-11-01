using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Horizon mask as configured: a minimum altitude, optionally refined by a drawn outline
/// enclosing the usable sky in image pixels. Pixels outside the outline are masked.
/// </summary>
[Requirement("REQ-22")]
public sealed record HorizonMaskDefinition
{
    /// <summary>Minimum altitude in degrees; sky below this is always masked.</summary>
    public double MinAltitudeDeg { get; init; } = 20.0;

    /// <summary>Polygon enclosing the usable sky in image pixels; empty when not drawn.</summary>
    public IReadOnlyList<PixelPoint> Outline { get; init; } = Array.Empty<PixelPoint>();
}
