using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Cloud detection thresholds. Defaults are conservative until tuned (REQ-43).</summary>
[Requirement("REQ-28", "REQ-29", "REQ-30", "REQ-31", "REQ-43")]
public sealed class CloudDetectionSettings
{
    public double LimitingMagnitude { get; set; } = 4.4;
    public double MinStarFraction { get; set; } = 0.70;
    public int MinExpectedStars { get; set; } = 8;
    public double MaxBrightnessRatio { get; set; } = 1.3;
    public double MaxUniformityRatio { get; set; } = 1.5;
    public double MoonExclusionDeg { get; set; } = 15.0;

    /// <summary>Set to true once thresholds have been tuned from real nights.</summary>
    public bool Tuned { get; set; }

    /// <summary>When true, detected cloud in the ROI makes the status unsafe (REQ-28). When false, cloud is advisory only.</summary>
    public bool DecidesSafety { get; set; } = false;
}
