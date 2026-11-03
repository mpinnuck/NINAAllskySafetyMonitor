using System.Globalization;
using System.Text;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// results.csv (Excel): a row per judged target per frame, and one row for a night frame that is not a sky
/// frame. Daylight frames are not written.
/// </summary>
[Requirement("REQ-42", "REQ-61")]
public sealed class ResultsCsvWriter : IDisposable
{
    internal const string Header = "Night,Local time,UTC time,File,Sun alt (°),Target,RA (h),Dec (°),ROI alt (°),ROI az (°),ROI inside (%),Stars expected,Stars found,Star fraction,Star test inconclusive,Relative brightness,Non-uniformity ratio,Brightness test skipped,Moon alt (°),Moon illumination,Verdict,Summary,Reference";

    private readonly StreamWriter writer;
    private readonly TimeZoneInfo zone;

    public ResultsCsvWriter(string path, TimeZoneInfo zone)
    {
        this.zone = zone ?? throw new ArgumentNullException(nameof(zone));
        writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine(Header);
    }

    public int Rows { get; private set; }

    public void Append(FrameResult r, bool referenceInUse)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (!r.IsNight) return;
        if (r.SkyFrame is { Passed: false } sky)
        {
            Write(Common(r).Concat(Enumerable.Repeat(string.Empty, 15)).Append("Not a sky frame").Append(sky.Reason).Append(string.Empty));
            return;
        }

        foreach (TargetResult t in r.Targets)
        {
            CloudAssessment c = t.Cloud;
            HorizontalCoordinate? centre = t.Roi.Centre;
            Write(Common(r).Concat(new[]
            {
                t.Target.Id,
                Num(t.Target.RaHours, "0.0"),
                Num(t.Target.DecDeg, "0"),
                Num(centre?.AltDeg, "0.0"),
                Num(centre?.AzDeg, "0.0"),
                Num(t.Roi.InsideFraction * 100, "0"),
                c.ExpectedStars.ToString(CultureInfo.InvariantCulture),
                c.DetectedStars.ToString(CultureInfo.InvariantCulture),
                Num(c.StarFraction, "0.000"),
                c.StarTestInconclusive ? "TRUE" : "FALSE",
                Num(c.RelativeBrightness, "0.000"),
                Num(c.UniformityRatio, "0.000"),
                c.BrightnessTestSkipped ? "TRUE" : "FALSE",
                Num(c.Moon?.Position.AltDeg, "0.0"),
                Num(c.Moon?.Illumination, "0.00"),
                t.Verdict.ToString(),
                c.Summary,
                r.ReferenceCaptured ? "Captured" : referenceInUse ? "In use" : string.Empty,
            }));
        }
    }

    public void Dispose() => writer.Dispose();

    private IEnumerable<string> Common(FrameResult r) => new[]
    {
        r.Frame.Night.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeZoneInfo.ConvertTimeFromUtc(r.Frame.CaptureUtc, zone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        r.Frame.CaptureUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        Path.GetFileName(r.Frame.Path),
        Num(r.SunAltDeg, "0.0"),
    };

    private void Write(IEnumerable<string> cells)
    {
        writer.WriteLine(string.Join(",", cells.Select(Quote)));
        Rows++;
    }

    internal static string Num(double? v, string format) =>
        v is double d && double.IsFinite(d) ? d.ToString(format, CultureInfo.InvariantCulture) : string.Empty;

    internal static string Quote(string v) =>
        v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : v;
}
