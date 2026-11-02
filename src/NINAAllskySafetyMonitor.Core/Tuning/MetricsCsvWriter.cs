using System.Globalization;
using System.Text;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Tuning;

/// <summary>
/// Appends one row of metrics per cycle to a tuning CSV (readable by Excel): time, state, reason, image
/// file, ROI, star test, brightness test, Moon and weather.
/// </summary>
[Requirement("REQ-41")]
public sealed class MetricsCsvWriter
{
    internal const string Header = "Local time,UTC time,State,Safe,Reason,Image file,Image captured (UTC),ROI Alt (°),ROI Az (°),ROI mode,Stars expected,Stars found,Star fraction,Star test inconclusive,Relative brightness,Non-uniformity ratio,Brightness test skipped,Cloud,Cloud summary,Moon alt (°),Moon illumination,Rain (mm/h),Gust (km/h)";

    public void Append(string path, MonitorSnapshot s, string imageFile, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(s);
        bool isNew = !File.Exists(path);
        var row = new List<string>
        {
            utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            s.Status.State.ToString(),
            s.Status.IsSafe ? "TRUE" : "FALSE",
            s.Status.Reason,
            imageFile,
            s.Image?.CaptureTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
            Num(s.Roi?.Centre?.AltDeg, "0.00"),
            Num(s.Roi?.Centre?.AzDeg, "0.00"),
            s.Roi is null ? string.Empty : s.Roi.UsesWholeWorkingArea ? "Whole working area" : "Circle",
        };

        CloudAssessment? c = s.Cloud;
        row.Add(c?.ExpectedStars.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        row.Add(c?.DetectedStars.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        row.Add(Num(c?.StarFraction, "0.000"));
        row.Add(c is null ? string.Empty : c.StarTestInconclusive ? "TRUE" : "FALSE");
        row.Add(Num(c?.RelativeBrightness, "0.000"));
        row.Add(Num(c?.UniformityRatio, "0.000"));
        row.Add(c is null ? string.Empty : c.BrightnessTestSkipped ? "TRUE" : "FALSE");
        row.Add(c is null ? string.Empty : c.CloudPresent ? "TRUE" : "FALSE");
        row.Add(c?.Summary ?? string.Empty);
        row.Add(Num(c?.Moon?.Position.AltDeg, "0.0"));
        row.Add(Num(c?.Moon?.Illumination, "0.00"));
        row.Add(Num(s.Weather?.PrecipRateMmH, "0.0"));
        row.Add(Num(s.Weather?.WindGustKmH, "0"));

        using var writer = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        if (isNew) writer.WriteLine(Header);
        writer.WriteLine(string.Join(",", row.Select(Quote)));
    }

    private static string Num(double? v, string format) =>
        v is double d && double.IsFinite(d) ? d.ToString(format, CultureInfo.InvariantCulture) : string.Empty;

    private static string Quote(string v) =>
        v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : v;
}
