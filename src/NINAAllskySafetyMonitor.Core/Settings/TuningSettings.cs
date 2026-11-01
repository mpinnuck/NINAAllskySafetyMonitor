using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Tuning mode settings.</summary>
[Requirement("REQ-41")]
public sealed class TuningSettings
{
    public bool Enabled { get; set; }

    public string OutputFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINAAllskySafetyMonitor", "Tuning");
}
