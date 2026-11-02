using System.Globalization;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Tuning;

/// <summary>
/// Tuning mode: saves every frame, and one CSV row of metrics per cycle, so the cloud thresholds can be
/// tuned from real nights. Files go in a folder per night (named by the local date the night starts, so a
/// night is not split at midnight): &lt;folder&gt;\yyyy-MM-dd\allsky-yyyyMMddHHmmss.jpg (capture time, UTC)
/// and tuning-yyyy-MM-dd.csv. A frame reused by a second cycle is saved once. Night folders older than
/// the retention period are deleted. Never throws: tuning must not affect the monitor.
/// </summary>
[Requirement("REQ-41")]
public sealed class TuningRecorder
{
    private readonly Func<TuningSettings> settings;
    private readonly IClock clock;
    private readonly ILogSink log;
    private readonly MetricsCsvWriter csv = new();
    private readonly object sync = new();
    private DateTime? lastPurgeNight;
    private bool failureLogged;

    public TuningRecorder(Func<TuningSettings> settings, IClock clock, ILogSink log)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The night a local time belongs to: the date twelve hours earlier.</summary>
    public static DateTime NightOf(DateTime local) => local.AddHours(-12).Date;

    /// <summary>Saves the frame and appends the metrics row, when tuning mode is on.</summary>
    public void Record(MonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        TuningSettings s = settings();
        if (!s.Enabled || snapshot.Status.State == SafetyState.Idle) return;

        try
        {
            lock (sync)
            {
                DateTime utc = clock.UtcNow;
                DateTime night = NightOf(utc.ToLocalTime());
                string folder = Path.Combine(s.OutputFolder, night.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(folder);
                Purge(s, night);

                string imageFile = string.Empty;
                if (snapshot.Image is SkyImage image)
                {
                    imageFile = $"allsky-{image.CaptureTimeUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}.jpg";
                    string imagePath = Path.Combine(folder, imageFile);
                    if (!File.Exists(imagePath)) File.WriteAllBytes(imagePath, image.JpegBytes);
                }

                string csvPath = Path.Combine(folder, $"tuning-{night.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv");
                csv.Append(csvPath, snapshot, imageFile, utc);
            }

            failureLogged = false;
        }
        catch (Exception e)
        {
            if (!failureLogged)
            {
                log.Write(LogLevel.Warning, $"Tuning data could not be saved: {e.Message}");
                failureLogged = true;
            }
        }
    }

    /// <summary>Deletes night folders older than the retention period, once per night.</summary>
    private void Purge(TuningSettings s, DateTime night)
    {
        if (lastPurgeNight == night) return;
        lastPurgeNight = night;

        DateTime oldestKept = night.AddDays(-(Math.Max(1, s.RetentionDays) - 1));
        foreach (string dir in Directory.EnumerateDirectories(s.OutputFolder))
        {
            string name = Path.GetFileName(dir);
            if (!DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day) || day >= oldestKept) continue;
            Directory.Delete(dir, recursive: true);
            log.Write(LogLevel.Info, $"Deleted tuning data for {name} (older than {s.RetentionDays} days)");
        }
    }
}
