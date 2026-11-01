using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Fallback site location, used when the NINA profile cannot supply one (default: station ISYDNEY478).</summary>
[Requirement("REQ-04")]
public sealed class SiteSettings
{
    public double LatitudeDeg { get; set; } = -33.713562;
    public double LongitudeDeg { get; set; } = 151.09024;
    public double ElevationM { get; set; } = 134.0;
}
