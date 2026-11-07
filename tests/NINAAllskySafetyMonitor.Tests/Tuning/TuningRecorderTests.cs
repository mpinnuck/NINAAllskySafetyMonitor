using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Logging;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Tuning;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Tuning;

[Requirement("REQ-41")]
public sealed class TuningRecorderTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "NASM-Tuning-" + Guid.NewGuid().ToString("N"));
    private readonly TuningSettings settings;
    private readonly FakeClock clock = new(new DateTime(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc));
    private readonly ListLogSink sink = new();

    public TuningRecorderTests()
    {
        settings = new TuningSettings { Enabled = true, OutputFolder = folder };
    }

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    private TuningRecorder Recorder(SecretRedactor? redactor = null) =>
        new(() => settings, clock, sink, redactor ?? new SecretRedactor(() => Array.Empty<string?>()));

    private string NightFolder => Path.Combine(folder, TuningRecorder.NightOf(clock.UtcNow.ToLocalTime()).ToString("yyyy-MM-dd"));

    private MonitorSnapshot Snapshot(DateTime captured, SafetyState state = SafetyState.Unsafe, byte[]? jpegBytes = null) => new(
        new SkyImage(jpegBytes ?? new byte[] { 0xFF, 0xD8, 7, 0xFF, 0xD9 }, captured, ImageSourceKind.Direct),
        new RegionOfInterest(new HorizontalCoordinate(70, 180), 15, false, 1, string.Empty),
        new CloudAssessment(11, 9, 9 / 11.0, 1.05, 1.1, false, false, false, "Clear: 9 of 11 stars (82%)"),
        new WeatherObservation(captured, 0, 9),
        new SafetyConditions(Array.Empty<ConditionCheck>()),
        new SafetyStatus(state, false, "All conditions clear: safe in 4:00", 240, OverrideMode.Auto, false, captured));

    [Fact]
    public void SavesEveryFrameOnceAndOneRowPerCycle()
    {
        TuningRecorder r = Recorder();
        DateTime t1 = clock.UtcNow.AddSeconds(-20);
        r.Record(Snapshot(t1));
        r.Record(Snapshot(t1)); // mount-triggered cycle on the same frame
        r.Record(Snapshot(t1.AddMinutes(1)));

        Assert.Equal(2, Directory.GetFiles(NightFolder, "allsky-*.jpg").Length);
        Assert.True(File.Exists(Path.Combine(NightFolder, $"allsky-{t1:yyyyMMddHHmmss}.jpg")));
        string[] lines = File.ReadAllLines(Directory.GetFiles(NightFolder, "tuning-*.csv").Single());
        Assert.Equal(4, lines.Length);
        Assert.StartsWith("Local time,UTC time,State,", lines[0]);
        Assert.Contains($",allsky-{t1:yyyyMMddHHmmss}.jpg,", lines[1]);
        Assert.Contains(",11,9,0.818,FALSE,1.050,1.100,FALSE,FALSE,Clear: 9 of 11 stars (82%),", lines[1]);
    }

    [Fact]
    public void SameTimestampFramesKeepDistinctAtomicImageFiles()
    {
        DateTime captured = clock.UtcNow.AddSeconds(-20);
        TuningRecorder recorder = Recorder();
        recorder.Record(Snapshot(captured, jpegBytes: new byte[] { 1, 2, 3 }));
        recorder.Record(Snapshot(captured, jpegBytes: new byte[] { 4, 5, 6 }));
        recorder.Record(Snapshot(captured, jpegBytes: new byte[] { 1, 2, 3 }));

        string[] images = Directory.GetFiles(NightFolder, "allsky-*.jpg").Order().ToArray();
        Assert.Equal(2, images.Length);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(NightFolder, $"allsky-{captured:yyyyMMddHHmmss}.jpg")));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(images.Single(path => path.EndsWith("-1.jpg", StringComparison.Ordinal))));
        Assert.Empty(Directory.GetFiles(NightFolder, "*.tmp"));

        string[] rows = File.ReadAllLines(Directory.GetFiles(NightFolder, "tuning-*.csv").Single());
        Assert.Equal(4, rows.Length);
        Assert.Contains($",allsky-{captured:yyyyMMddHHmmss}.jpg,", rows[1]);
        Assert.Contains($",allsky-{captured:yyyyMMddHHmmss}-1.jpg,", rows[2]);
        Assert.Contains($",allsky-{captured:yyyyMMddHHmmss}.jpg,", rows[3]);
    }

    [Fact]
    public void MetricsCsvRedactsSecretsAndPreventsFormulaInjection()
    {
        const string secret = "api-secret-123";
        var redactor = new SecretRedactor(() => new[] { secret });
        MonitorSnapshot snapshot = Snapshot(clock.UtcNow) with
        {
            Status = Snapshot(clock.UtcNow).Status with { Reason = "=" + secret },
            Cloud = Snapshot(clock.UtcNow).Cloud! with { Summary = "@" + secret },
        };

        Recorder(redactor).Record(snapshot);

        string line = File.ReadAllLines(Directory.GetFiles(NightFolder, "tuning-*.csv").Single())[1];
        Assert.DoesNotContain(secret, line);
        Assert.Contains("'=***", line);
        Assert.Contains("'@***", line);
    }

    [Fact]
    public void NothingIsSavedWhenOff()
    {
        settings.Enabled = false;
        Recorder().Record(Snapshot(clock.UtcNow));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void NothingIsSavedOutsideTheWindow()
    {
        Recorder().Record(Snapshot(clock.UtcNow, SafetyState.Idle));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void NightFolderDoesNotSplitAtMidnight()
    {
        Assert.Equal(new DateTime(2026, 11, 5), TuningRecorder.NightOf(new DateTime(2026, 11, 5, 23, 30, 0)));
        Assert.Equal(new DateTime(2026, 11, 5), TuningRecorder.NightOf(new DateTime(2026, 11, 6, 3, 30, 0)));
    }

    [Fact]
    public void NightsOlderThanRetentionAreDeleted()
    {
        DateTime night = TuningRecorder.NightOf(clock.UtcNow.ToLocalTime());
        string old = Path.Combine(folder, night.AddDays(-30).ToString("yyyy-MM-dd"));
        string kept = Path.Combine(folder, night.AddDays(-29).ToString("yyyy-MM-dd"));
        string other = Path.Combine(folder, "my notes");
        foreach (string d in new[] { old, kept, other }) Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(old, "allsky-1.jpg"), "x");

        Recorder().Record(Snapshot(clock.UtcNow));

        Assert.False(Directory.Exists(old));
        Assert.True(Directory.Exists(kept));
        Assert.True(Directory.Exists(other));
        Assert.Contains(sink.Entries, e => e.Message.StartsWith("Deleted tuning data for ", StringComparison.Ordinal));
    }

    [Fact]
    public void UnwritableFolderDoesNotThrow()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        File.WriteAllText(folder, "x");
        try
        {
            Recorder().Record(Snapshot(clock.UtcNow));
            Assert.Single(sink.Entries, e => e.Message.StartsWith("Tuning data could not be saved", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(folder);
        }
    }
}
