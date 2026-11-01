using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Region of interest settings.</summary>
[Requirement("REQ-23")]
public sealed class RoiSettings
{
    /// <summary>ROI radius on the sky, in degrees.</summary>
    public double RadiusDeg { get; set; } = 15.0;
}
