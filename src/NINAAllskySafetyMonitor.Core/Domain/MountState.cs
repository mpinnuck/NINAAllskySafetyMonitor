using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>
/// Mount state taken from NINA. RA/Dec are null when the mount reports no valid position.
/// The reported Alt/Az are kept for logging only and are never used for positioning (REQ-06).
/// </summary>
[Requirement("REQ-05", "REQ-06", "REQ-07")]
public sealed record MountState(
    bool Connected,
    bool AtPark,
    bool Tracking,
    bool Slewing,
    double? RaHours,
    double? DecDeg,
    CoordinateEpoch Epoch,
    double? ReportedAltDeg,
    double? ReportedAzDeg,
    DateTime TimestampUtc)
{
    /// <summary>True when RA and Dec are present, finite and within range.</summary>
    public bool HasValidPosition =>
        RaHours is double ra && DecDeg is double dec &&
        double.IsFinite(ra) && double.IsFinite(dec) &&
        ra >= 0.0 && ra < 24.0 && dec >= -90.0 && dec <= 90.0;
}
