using System.Globalization;
using ClosedXML.Excel;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// review.xlsx: a Review sheet with one row per marked-up copy (night, time, target, verdict, metrics,
/// why it was picked, a link to the image, a "My verdict" drop-down and an Agrees formula), a Summary
/// sheet whose counts update as verdicts are entered, and a Settings sheet with the thresholds used.
/// </summary>
[Requirement("REQ-61")]
public sealed class ReviewWorkbookWriter
{
    private static readonly string[] Columns =
    {
        "Night", "Local time", "Target", "Verdict", "Stars found", "Stars expected", "Star fraction",
        "Relative brightness", "Non-uniformity ratio", "Picked because", "Image", "My verdict", "Agrees",
    };

    private readonly List<object?[]> rows = new();
    private readonly TimeZoneInfo zone;

    public ReviewWorkbookWriter(TimeZoneInfo zone)
    {
        this.zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    public int Count => rows.Count;

    public void Add(SavedFrame frame, TargetResult t, string why, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(t);
        rows.Add(new object?[]
        {
            frame.Night.ToDateTime(TimeOnly.MinValue),
            TimeZoneInfo.ConvertTimeFromUtc(frame.CaptureUtc, zone),
            t.Target.Id,
            t.Verdict.ToString(),
            t.Cloud.DetectedStars,
            t.Cloud.ExpectedStars,
            t.Cloud.StarFraction,
            t.Cloud.RelativeBrightness,
            t.Cloud.UniformityRatio,
            why,
            imagePath,
        });
    }

    public void Save(string path, MonitorSettings settings, ReplayOptions options, string referenceText)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(options);
        using var wb = new XLWorkbook();
        string workbookFolder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        wb.Style.Font.FontName = "Arial";
        wb.Style.Font.FontSize = 10;
        IXLWorksheet review = wb.AddWorksheet("Review");
        IXLWorksheet summary = wb.AddWorksheet("Summary");
        IXLWorksheet used = wb.AddWorksheet("Settings");

        for (int col = 0; col < Columns.Length; col++) review.Cell(1, col + 1).Value = Columns[col];
        review.Row(1).Style.Font.Bold = true;
        review.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
        int last = rows.Count + 1;
        for (int r = 0; r < rows.Count; r++)
        {
            object?[] v = rows[r];
            int row = r + 2;
            review.Cell(row, 1).Value = (DateTime)v[0]!;
            review.Cell(row, 2).Value = (DateTime)v[1]!;
            review.Cell(row, 3).Value = (string)v[2]!;
            review.Cell(row, 4).Value = (string)v[3]!;
            review.Cell(row, 5).Value = (int)v[4]!;
            review.Cell(row, 6).Value = (int)v[5]!;
            SetNumber(review.Cell(row, 7), (double?)v[6]);
            SetNumber(review.Cell(row, 8), (double?)v[7]);
            SetNumber(review.Cell(row, 9), (double?)v[8]);
            review.Cell(row, 10).Value = (string)v[9]!;
            string imagePath = (string)v[10]!;
            if (!Path.IsPathRooted(imagePath)) imagePath = Path.GetFullPath(Path.Combine(workbookFolder, imagePath));
            imagePath = imagePath.Replace("\"", "\"\"", StringComparison.Ordinal);
            review.Cell(row, 11).FormulaA1 = $"HYPERLINK(\"{imagePath}\",\"open\")";
            review.Cell(row, 13).FormulaA1 = $"IF(L{row}=\"\",\"\",IF(L{row}=\"Unsure\",\"\",IF(L{row}=D{row},\"Yes\",\"No\")))";
        }

        review.Column(1).Style.NumberFormat.Format = "yyyy-mm-dd";
        review.Column(2).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm:ss";
        review.Range(2, 7, Math.Max(2, last), 9).Style.NumberFormat.Format = "0.00";
        review.Column(11).Style.Font.FontColor = XLColor.Blue;
        if (rows.Count > 0)
        {
            IXLRange mine = review.Range(2, 12, last, 12);
            mine.CreateDataValidation().List("\"Clear,Cloud,Unsure\"", true);
            mine.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
            review.Range(1, 1, last, Columns.Length).SetAutoFilter();
        }

        review.SheetView.FreezeRows(1);
        review.Columns().AdjustToContents(1, Math.Min(last, 200));
        review.Column(10).Width = 36;

        string mv = $"Review!$L$2:$L${Math.Max(2, last)}", ag = $"Review!$M$2:$M${Math.Max(2, last)}", vd = $"Review!$D$2:$D${Math.Max(2, last)}";
        var lines = new (string Label, string Formula)[]
        {
            ("Frames to review", $"COUNTA(Review!$C$2:$C${Math.Max(2, last)})"),
            ("Reviewed (My verdict entered)", $"COUNTA({mv})"),
            ("Agree", $"COUNTIF({ag},\"Yes\")"),
            ("Disagree", $"COUNTIF({ag},\"No\")"),
            ("Agreement", "IFERROR(B4/(B4+B5),\"\")"),
            ("Monitor said Cloud, you said Clear (false cloud)", $"COUNTIFS({vd},\"Cloud\",{mv},\"Clear\")"),
            ("Monitor said Clear, you said Cloud (missed cloud)", $"COUNTIFS({vd},\"Clear\",{mv},\"Cloud\")"),
            ("Monitor said Unknown", $"COUNTIF({vd},\"Unknown\")"),
        };
        summary.Cell(1, 1).Value = "Replay review summary";
        summary.Cell(1, 1).Style.Font.Bold = true;
        for (int i = 0; i < lines.Length; i++)
        {
            summary.Cell(i + 2, 1).Value = lines[i].Label;
            summary.Cell(i + 2, 2).FormulaA1 = lines[i].Formula;
        }

        summary.Cell(6, 2).Style.NumberFormat.Format = "0%";
        summary.Cell(11, 1).Value = "Enter Clear, Cloud or Unsure in the yellow My verdict column of the Review sheet; these counts update as you go.";
        summary.Cell(11, 1).Style.Font.Italic = true;
        summary.Column(1).Width = 52;

        CloudDetectionSettings c = settings.Cloud;
        var settingsRows = new (string, object)[]
        {
            ("Frames folder", options.FramesFolder),
            ("Settings file", options.SettingsFile),
            ("Nights", $"{options.FirstNight?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "first"} to {options.LastNight?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "last"}"),
            ("Frame step (s)", options.StepS),
            ("Targets", $"Dec {options.DecFromDeg:+0;-0} to {options.DecToDeg:+0;-0} every {options.DecStepDeg}°, RA every {options.RaStepH} h"),
            ("Sun below (°)", settings.Window.SunAltitudeLimitDeg),
            ("ROI radius (°)", settings.Roi.RadiusDeg),
            ("Limiting magnitude", c.LimitingMagnitude),
            ("Cloud below star fraction", c.MinStarFraction),
            ("Minimum expected stars", c.MinExpectedStars),
            ("Cloud above brightness (×)", c.MaxBrightnessRatio),
            ("Cloud above non-uniformity (×)", c.MaxUniformityRatio),
            ("Moon exclusion (°)", c.MoonExclusionDeg),
            ("Near-limit band", options.NearLimit),
            ("Clear-sky reference", referenceText),
        };
        for (int i = 0; i < settingsRows.Length; i++)
        {
            used.Cell(i + 1, 1).Value = settingsRows[i].Item1;
            used.Cell(i + 1, 2).Value = XLCellValue.FromObject(settingsRows[i].Item2);
        }

        used.Column(1).Width = 32;
        used.Column(2).Width = 60;
        wb.SaveAs(path);
    }

    private static void SetNumber(IXLCell cell, double? v)
    {
        if (v is double d && double.IsFinite(d)) cell.Value = d;
    }
}
