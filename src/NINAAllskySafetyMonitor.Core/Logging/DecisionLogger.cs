using System.Globalization;
using System.Text;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Logging;

/// <summary>
/// Rolling decision log: one CSV file per local day (decisions-yyyy-MM-dd.csv, readable by Excel) with
/// a row for every decision: state, safe, override, countdown, reason and the metrics behind it, including
/// the mount position and the ROI. The mount-reported Alt/Az are logged for diagnostics only (REQ-06). A
/// change of state is marked. Files older than the retention period are deleted. Secrets are redacted.
/// </summary>
[Requirement("REQ-06", "REQ-44", "REQ-54")]
public sealed class DecisionLogger
{
    private const string Header = "Local time,UTC time,Event,State,Safe,Override,Seconds to safe,Reason,Image captured (UTC),Image age (min),Image source,Rain (mm/h),Gust (km/h),Weather age (min),Mount,Mount RA (h),Mount Dec (°),Mount reported Alt (°),Mount reported Az (°),ROI Alt (°),ROI Az (°),ROI mode,ROI inside (%),ROI note";
    private readonly Func<LoggingSettings> settings;
    private readonly IClock clock;
    private readonly SecretRedactor redactor;
    private readonly ILogSink log;
    private readonly object sync = new();
    private SafetyState? lastState;
    private DateTime? lastPurgeDay;
    private bool writeFailureLogged;

    public DecisionLogger(Func<LoggingSettings> settings, IClock clock, SecretRedactor redactor, ILogSink log)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>File name for a local date.</summary>
    public static string FileName(DateTime localDate) => $"decisions-{localDate:yyyy-MM-dd}.csv";

    /// <summary>Appends one row. Never throws: a log failure must not affect the monitor.</summary>
    public void Write(MonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        try
        {
            lock (sync) WriteRow(snapshot);
            writeFailureLogged = false;
        }
        catch (Exception e)
        {
            if (!writeFailureLogged)
            {
                log.Write(LogLevel.Warning, $"Decision log could not be written: {e.Message}");
                writeFailureLogged = true;
            }
        }
    }

    private void WriteRow(MonitorSnapshot snapshot)
    {
        LoggingSettings s = settings();
        DateTime utc = clock.UtcNow;
        DateTime local = utc.ToLocalTime();
        Directory.CreateDirectory(s.Folder);

        Purge(s, local.Date);

        string path = Path.Combine(s.Folder, FileName(local.Date));
        bool isNew = !File.Exists(path);
        SafetyStatus st = snapshot.Status;
        string evt = lastState is null ? "Start" : lastState != st.State ? "State change" : string.Empty;
        lastState = st.State;

        var row = new StringBuilder();
        Append(row, local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Append(row, utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Append(row, evt);
        Append(row, st.State.ToString());
        Append(row, st.IsSafe ? "TRUE" : "FALSE");
        Append(row, st.Override.ToString());
        Append(row, st.SecondsToSafe?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        Append(row, redactor.Redact(st.Reason));

        SkyImage? img = snapshot.Image;
        Append(row, img?.CaptureTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty);
        Append(row, img is null ? string.Empty : Number((utc - img.CaptureTimeUtc).TotalMinutes, "0.0"));
        Append(row, img?.Source.ToString() ?? string.Empty);

        WeatherObservation? w = snapshot.Weather;
        Append(row, w is null ? string.Empty : Number(w.PrecipRateMmH, "0.0"));
        Append(row, w is null ? string.Empty : Number(w.WindGustKmH, "0"));
        Append(row, w is null ? string.Empty : Number((utc - w.ObservationTimeUtc).TotalMinutes, "0.0"));

        MountState? m = snapshot.Mount;
        Append(row, MountSummary(m));
        Append(row, Signed(m?.RaHours, "0.0000"));
        Append(row, Signed(m?.DecDeg, "0.000"));
        Append(row, Signed(m?.ReportedAltDeg, "0.00"));
        Append(row, Signed(m?.ReportedAzDeg, "0.00"));

        RegionOfInterest? roi = snapshot.Roi;
        Append(row, Signed(roi?.Centre?.AltDeg, "0.00"));
        Append(row, Signed(roi?.Centre?.AzDeg, "0.00"));
        Append(row, roi is null ? string.Empty : roi.UsesWholeWorkingArea ? "Whole working area" : "Circle");
        Append(row, roi?.Centre is null ? string.Empty : Number(roi.InsideFraction * 100, "0"));
        Append(row, roi?.Note ?? string.Empty, last: true);

        using var writer = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        if (isNew) writer.WriteLine(Header);
        writer.WriteLine(row.ToString());
    }

    /// <summary>Deletes decision files older than the retention period, once per local day.</summary>
    private void Purge(LoggingSettings s, DateTime today)
    {
        if (lastPurgeDay == today) return;
        lastPurgeDay = today;

        DateTime oldestKept = today.AddDays(-(Math.Max(1, s.RetentionDays) - 1));
        foreach (string file in Directory.EnumerateFiles(s.Folder, "decisions-*.csv"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(name["decisions-".Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day)
                && day < oldestKept)
            {
                File.Delete(file);
                log.Write(LogLevel.Info, $"Deleted old decision log {Path.GetFileName(file)} (older than {s.RetentionDays} days)");
            }
        }
    }

    /// <summary>"Not connected", "Parked", "Slewing", "Tracking" or "Not tracking"; empty without mount information.</summary>
    internal static string MountSummary(MountState? m) => m switch
    {
        null => string.Empty,
        { Connected: false } => "Not connected",
        { AtPark: true } => "Parked",
        { Slewing: true } => "Slewing",
        { Tracking: true } => "Tracking",
        _ => "Not tracking",
    };

    private static string Signed(double? v, string format) => v is double d && double.IsFinite(d) ? d.ToString(format, CultureInfo.InvariantCulture) : string.Empty;

    private static string Number(double v, string format) => Math.Max(0, v).ToString(format, CultureInfo.InvariantCulture);

    private static void Append(StringBuilder row, string value, bool last = false)
    {
        bool quote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        row.Append(quote ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value);
        if (!last) row.Append(',');
    }
}
