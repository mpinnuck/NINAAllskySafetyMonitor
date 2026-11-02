using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Where the Moon is and how much of it is lit.</summary>
/// <param name="Position">Topocentric altitude and azimuth.</param>
/// <param name="Illumination">Illuminated fraction, 0 (new) to 1 (full).</param>
[Requirement("REQ-31")]
public sealed record MoonState(HorizontalCoordinate Position, double Illumination)
{
    /// <summary>True when the Moon is above the horizon.</summary>
    public bool IsUp => Position.AltDeg > 0;
}
