using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Lens calibration settings.</summary>
[Requirement("REQ-20")]
public sealed class LensFitSettings
{
    /// <summary>A fit matching fewer stars than this is reported with a warning.</summary>
    public int MinMatchedStars { get; set; } = 15;

    /// <summary>A fit whose RMS error on the sky is above this, in degrees, is reported with a warning.</summary>
    public double MaxFitErrorDeg { get; set; } = 0.2;

    /// <summary>Faintest catalogue stars used in the fit (the catalogue holds stars to magnitude 4.5).</summary>
    public double CatalogueLimitMagnitude { get; set; } = 4.5;
}
