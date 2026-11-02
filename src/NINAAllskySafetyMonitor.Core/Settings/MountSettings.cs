using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Mount watching settings.</summary>
[Requirement("REQ-27")]
public sealed class MountSettings
{
    /// <summary>How often the mount state is read, in seconds. A change (slew, park, tracking) re-evaluates at once.</summary>
    public int PollIntervalS { get; set; } = 2;
}
