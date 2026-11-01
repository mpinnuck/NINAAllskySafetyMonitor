using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Observing site.</summary>
/// <param name="LatitudeDeg">Latitude in degrees, north positive.</param>
/// <param name="LongitudeDeg">Longitude in degrees, east positive.</param>
/// <param name="ElevationM">Elevation in metres.</param>
[Requirement("REQ-04")]
public sealed record SiteLocation(double LatitudeDeg, double LongitudeDeg, double ElevationM);
