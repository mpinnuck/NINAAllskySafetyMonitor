using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Calibration results stored with the settings.</summary>
[Requirement("REQ-57")]
public sealed class CalibrationData
{
    /// <summary>Lens calibration, or null until calibrated.</summary>
    public LensCalibration? Lens { get; set; }

    public HorizonMaskDefinition Mask { get; set; } = new();

    /// <summary>Clear-sky reference, or null until captured.</summary>
    public ClearSkyReference? ClearSkyReference { get; set; }
}
