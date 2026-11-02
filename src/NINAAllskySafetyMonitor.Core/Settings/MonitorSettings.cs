using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Root of all configurable values, persisted as one JSON file.</summary>
[Requirement("REQ-55")]
public sealed class MonitorSettings
{
    /// <summary>Settings file format version, for future migrations.</summary>
    public int SchemaVersion { get; set; } = 1;

    public SiteSettings Site { get; set; } = new();
    public OperatingWindowSettings Window { get; set; } = new();
    public ImageSourceSettings ImageSource { get; set; } = new();
    public RoiSettings Roi { get; set; } = new();
    public MountSettings Mount { get; set; } = new();
    public CloudDetectionSettings Cloud { get; set; } = new();
    public WeatherSettings Weather { get; set; } = new();
    public DecisionSettings Decision { get; set; } = new();
    public TuningSettings Tuning { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public CalibrationData Calibration { get; set; } = new();
}
