using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Calibration results stored with the settings.</summary>
[Requirement("REQ-57")]
public sealed class CalibrationData
{
    /// <summary>Lens model, or null until calibrated; cloud assessment requires a lens model.</summary>
    public LensCalibration? Lens { get; set; }

    /// <summary>The part of the sky the monitor assesses.</summary>
    public WorkingAreaDefinition WorkingArea { get; set; } = new();

    /// <summary>Clear-sky reference, or null until captured.</summary>
    public ClearSkyReference? ClearSkyReference { get; set; }
}
