using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Decision;

/// <summary>While the mount is slewing the safety state is held; the ROI moves once the slew has finished.</summary>
[Requirement("REQ-27")]
public sealed class SlewHoldPolicy
{
    public bool ShouldHold(MountState? m) => m is { Connected: true, Slewing: true };
}
