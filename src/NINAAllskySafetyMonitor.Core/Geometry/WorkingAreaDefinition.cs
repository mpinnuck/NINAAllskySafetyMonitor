using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// The part of the sky the monitor can see and assess: a circle on the sky, optionally reduced by a
/// drawn outline (image pixels) around trees, the house and the overlay text.
/// </summary>
[Requirement("REQ-22", "REQ-57")]
public sealed record WorkingAreaDefinition
{
    /// <summary>Altitude of the circle's centre, in degrees (75 = 15° south of the zenith with azimuth 180).</summary>
    public double CentreAltDeg { get; init; } = 75.0;

    /// <summary>Azimuth of the circle's centre, in degrees from north through east.</summary>
    public double CentreAzDeg { get; init; } = 180.0;

    /// <summary>Radius of the circle on the sky, in degrees.</summary>
    public double RadiusDeg { get; init; } = 60.0;

    /// <summary>Polygon enclosing the usable sky in image pixels; empty when not drawn.</summary>
    public IReadOnlyList<PixelPoint> Outline { get; init; } = Array.Empty<PixelPoint>();

    public HorizontalCoordinate Centre => new(CentreAltDeg, CentreAzDeg);
}
