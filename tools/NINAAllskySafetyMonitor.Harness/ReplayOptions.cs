using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>What to replay and how, from the command line.</summary>
[Requirement("REQ-42", "REQ-61")]
public sealed record ReplayOptions
{
    public string FramesFolder { get; init; } = string.Empty;
    public string SettingsFile { get; init; } = string.Empty;
    public string OutputFolder { get; init; } = string.Empty;

    /// <summary>First and last night (the date the night started), or null for all.</summary>
    public DateOnly? FirstNight { get; init; }
    public DateOnly? LastNight { get; init; }

    /// <summary>Minimum seconds between frames used, like the plugin's cycle interval.</summary>
    public int StepS { get; init; } = 60;

    public double DecFromDeg { get; init; } = -45;
    public double DecToDeg { get; init; } = 45;
    public double DecStepDeg { get; init; } = 10;
    public double RaStepH { get; init; } = 1;

    /// <summary>A metric within this fraction of its limit is "near the limit" and kept for review.</summary>
    public double NearLimit { get; init; } = 0.10;

    /// <summary>Use the clear-sky reference in the settings file instead of capturing one.</summary>
    public bool UseSettingsReference { get; init; }

    /// <summary>Pause after each frame, in milliseconds, for watching it run.</summary>
    public int DelayMs { get; init; }

    /// <summary>Time zone of the times in the frame names (default: this computer's).</summary>
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;
}
