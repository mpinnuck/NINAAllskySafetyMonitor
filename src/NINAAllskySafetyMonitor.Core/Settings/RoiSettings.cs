using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Region of interest settings.</summary>
[Requirement("REQ-23", "REQ-58")]
public sealed class RoiSettings
{
    /// <summary>ROI radius on the sky, in degrees.</summary>
    public double RadiusDeg { get; set; } = 15.0;

    /// <summary>Below this fraction of the ROI inside the working area, the whole working area is judged.</summary>
    public double MinInsideFraction { get; set; } = 0.5;
}
