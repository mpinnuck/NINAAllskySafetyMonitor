using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Operating window: the monitor is active only while the Sun is below this altitude.</summary>
[Requirement("REQ-09")]
public sealed class OperatingWindowSettings
{
    /// <summary>Sun altitude limit in degrees (−18 = astronomical twilight).</summary>
    public double SunAltitudeLimitDeg { get; set; } = -18.0;
}
