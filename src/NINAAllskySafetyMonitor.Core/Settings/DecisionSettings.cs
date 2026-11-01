using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Safety decision settings.</summary>
[Requirement("REQ-39")]
public sealed class DecisionSettings
{
    /// <summary>Minutes all conditions must hold before reporting safe.</summary>
    public int SafeAfterMin { get; set; } = 4;
}
