using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Decision log settings.</summary>
[Requirement("REQ-44")]
public sealed class LoggingSettings
{
    public int RetentionDays { get; set; } = 7;

    public string Folder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINAAllskySafetyMonitor", "Logs");
}
