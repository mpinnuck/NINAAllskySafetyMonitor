using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Raises the brightness-test limits while the Moon is up: factor = 1 + <see cref="Weight"/> ×
/// illumination × sin(altitude). A full Moon overhead raises them by half; a Moon below the horizon or new
/// leaves them unchanged.
/// </summary>
[Requirement("REQ-31")]
public sealed class MoonBrightnessAdjuster
{
    /// <summary>How much a full Moon at the zenith raises the limits.</summary>
    public const double Weight = 0.5;

    public double Factor(double moonAltDeg, double illumination) =>
        1.0 + (Weight * Math.Clamp(illumination, 0, 1) * Math.Max(0, Math.Sin(moonAltDeg * Math.PI / 180.0)));
}
