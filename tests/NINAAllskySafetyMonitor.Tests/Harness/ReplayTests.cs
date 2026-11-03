using ClosedXML.Excel;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Harness;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Harness;

/// <summary>End to end: synthetic night frames through the harness, as from the command line.</summary>
[Requirement("REQ-42", "REQ-61")]
public sealed class ReplayTests : IDisposable
{
    private readonly ReplayFixture fx = new();

    public void Dispose() => fx.Dispose();

    [Fact]
    public void DecoderGivesTheDarkestChannel()
    {
        GrayFrame original = SyntheticCloudSky.Frame();
        GrayFrame decoded = new SkiaFrameDecoder().Decode(ReplayFixture.Jpeg(original));
        Assert.Equal((original.Width, original.Height), (decoded.Width, decoded.Height));
        double meanError = Enumerable.Range(0, 2000).Average(i => Math.Abs(decoded.Pixels[i * 499] - original.Pixels[i * 499]));
        Assert.InRange(meanError, 0, 2);
    }

    [Fact]
    public void ReplayWritesResultsReviewWorkbookAndMarkedUpFrames()
    {
        DateTime t0 = SyntheticSky.FrameUtc.AddHours(1); // 03:21 local: dark, and the Moon has set
        fx.Save(SyntheticCloudSky.Frame(utc: t0), t0);
        fx.Save(SyntheticCloudSky.Frame(cloudCentre: SyntheticCloudSky.SouthHigh, cloudRadiusDeg: 25, seed: 4, utc: t0.AddMinutes(1)), t0.AddMinutes(1));
        fx.Save(SyntheticCloudSky.Frame(seed: 5, utc: t0.AddMinutes(2)), t0.AddMinutes(2));
        fx.Save(SyntheticCloudSky.NoticeImage(800, 600), t0.AddMinutes(3));
        fx.Save(SyntheticCloudSky.Frame(), t0.AddHours(9)); // daylight: skipped

        Assert.Equal(0, NINAAllskySafetyMonitor.Harness.Program.Replay(fx.Options()));

        string[] csv = File.ReadAllLines(Path.Combine(fx.Out, "results.csv"));
        Assert.StartsWith("Night,Local time,UTC time,File,", csv[0]);
        Assert.Contains(csv, l => l.Contains(",Cloud,", StringComparison.Ordinal));
        Assert.Contains(csv, l => l.Contains(",Clear,", StringComparison.Ordinal));
        Assert.Contains(csv, l => l.Contains(",Captured", StringComparison.Ordinal));
        Assert.Contains(csv, l => l.Contains("Not a sky frame", StringComparison.Ordinal));
        Assert.DoesNotContain(csv, l => l.Contains("allsky-20261020122133", StringComparison.Ordinal));
        Assert.Contains(csv, l => l.Contains(",In use", StringComparison.Ordinal)); // later frames use the captured reference

        using var wb = new XLWorkbook(Path.Combine(fx.Out, "review.xlsx"));
        IXLWorksheet review = wb.Worksheet("Review");
        Assert.Equal("My verdict", review.Cell(1, 12).GetString());
        int rows = review.LastRowUsed()!.RowNumber() - 1;
        Assert.True(rows > 0);
        Assert.Contains("Clear → Cloud", review.Column(10).CellsUsed().Select(c => c.GetString()));
        string link = review.Cell(2, 11).FormulaA1;
        Assert.StartsWith("HYPERLINK(\"review/", link);
        string image = link.Split('"')[1];
        Assert.True(File.Exists(Path.Combine(fx.Out, image)), image);
        Assert.Equal("Agree", wb.Worksheet("Summary").Cell(4, 1).GetString());
        Assert.Equal(Directory.GetFiles(Path.Combine(fx.Out, "review"), "*.jpg", SearchOption.AllDirectories).Length, rows);
    }

    [Fact]
    public void OptionsAreParsed()
    {
        ReplayOptions o = NINAAllskySafetyMonitor.Harness.Program.Parse(new[]
        {
            "--frames", "/f", "--settings", "/s.json", "--out", "/o", "--nights", "20261019-20261101", "--step", "30",
            "--dec", "-30:30:15", "--ra-step", "2", "--near", "0.2", "--use-settings-reference", "--zone", "Australia/Sydney",
        });

        Assert.Equal(("/f", "/s.json", "/o", 30), (o.FramesFolder, o.SettingsFile, o.OutputFolder, o.StepS));
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 11, 1)), (o.FirstNight!.Value, o.LastNight!.Value));
        Assert.Equal((-30.0, 30.0, 15.0, 2.0, 0.2), (o.DecFromDeg, o.DecToDeg, o.DecStepDeg, o.RaStepH, o.NearLimit));
        Assert.True(o.UseSettingsReference);
        Assert.Throws<ArgumentException>(() => NINAAllskySafetyMonitor.Harness.Program.Parse(new[] { "--frames", "/f" }));
    }
}
