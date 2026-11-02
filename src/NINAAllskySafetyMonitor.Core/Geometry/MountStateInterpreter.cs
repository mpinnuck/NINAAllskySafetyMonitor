using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>Decides whether the mount's position can place the ROI, and if not, why.</summary>
[Requirement("REQ-07", "REQ-26")]
public sealed class MountStateInterpreter
{
    /// <summary>Null when usable; otherwise a short reason ("Mount parked", …).</summary>
    public string? WhyUnusable(MountState? m) => m switch
    {
        null => "No mount information",
        { Connected: false } => "Mount not connected",
        { AtPark: true } => "Mount parked",
        { Tracking: false } => "Mount not tracking",
        { HasValidPosition: false } => "Mount position unavailable",
        _ => null,
    };

    public bool IsUsableForRoi(MountState? m) => WhyUnusable(m) is null;
}
