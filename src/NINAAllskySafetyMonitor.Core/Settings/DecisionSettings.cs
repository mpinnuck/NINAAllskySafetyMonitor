using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Safety decision settings.</summary>
[Requirement("REQ-39", "REQ-50")]
public sealed class DecisionSettings
{
    /// <summary>Minutes all conditions must hold before reporting safe.</summary>
    public int SafeAfterMin { get; set; } = 4;

    /// <summary>
    /// When a condition can't be determined (no usable image, cloud unknown, no current weather data),
    /// report safe instead of unsafe. Detected cloud, rain or wind stays unsafe.
    /// </summary>
    public bool UndeterminedIsSafe { get; set; }
}
