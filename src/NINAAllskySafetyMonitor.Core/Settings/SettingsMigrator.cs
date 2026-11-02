using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>
/// Brings a settings file written by an earlier version up to date, so changed defaults reach saved
/// files. Version 2 (slice 6): the limiting magnitude default rose from 4.0 to 4.4; a file still holding
/// the old default gets the new one, while a value the imager chose is kept.
/// </summary>
[Requirement("REQ-29", "REQ-55")]
public static class SettingsMigrator
{
    /// <summary>The current settings file version.</summary>
    public const int CurrentVersion = 2;

    public static MonitorSettings Migrate(MonitorSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.SchemaVersion < 2)
        {
            if (s.Cloud.LimitingMagnitude == 4.0) s.Cloud.LimitingMagnitude = 4.4;
        }

        s.SchemaVersion = Math.Max(s.SchemaVersion, CurrentVersion);
        return s;
    }
}
