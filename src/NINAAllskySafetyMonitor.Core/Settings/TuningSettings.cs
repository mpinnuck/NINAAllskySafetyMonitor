using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Tuning mode settings: every frame and its metrics are saved for tuning the cloud thresholds.</summary>
[Requirement("REQ-41")]
public sealed class TuningSettings
{
    public bool Enabled { get; set; }

    /// <summary>Saved frames and metrics older than this many days are deleted.</summary>
    public int RetentionDays { get; set; } = 30;

    public string OutputFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINAAllskySafetyMonitor", "Tuning");
}
