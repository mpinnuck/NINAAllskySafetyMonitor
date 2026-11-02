using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Logging;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Logging;

[Requirement("REQ-06", "REQ-44", "REQ-54")]
public sealed class DecisionLoggerTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "allsky-decisions-" + Guid.NewGuid().ToString("N"));
    private readonly LoggingSettings settings;
    private readonly FakeClock clock = new(new DateTime(2026, 11, 1, 11, 0, 0, DateTimeKind.Utc));

    public DecisionLoggerTests() => settings = new LoggingSettings { Folder = folder, RetentionDays = 7 };

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    private readonly ListLogSink sink = new();

    private DecisionLogger Logger(params string[] secrets) =>
        new(() => settings, clock, new SecretRedactor(() => secrets), sink);

    private MonitorSnapshot Snapshot(SafetyState state, string reason) =>
        new(new SkyImage(new byte[] { 1 }, clock.UtcNow.AddMinutes(-1), ImageSourceKind.Direct), null, null,
            new WeatherObservation(clock.UtcNow.AddMinutes(-2), 0.0, 7.2), new SafetyConditions(Array.Empty<ConditionCheck>()),
            new SafetyStatus(state, state == SafetyState.Safe, reason, null, OverrideMode.Auto, false, clock.UtcNow));

    private string TodayFile() => Path.Combine(folder, DecisionLogger.FileName(clock.UtcNow.ToLocalTime().Date));

    [Fact]
    public void WritesHeaderAndOneRowPerDecisionWithMetrics()
    {
        DecisionLogger log = Logger();
        log.Write(Snapshot(SafetyState.Unsafe, "Cloud detection not implemented yet"));
        log.Write(Snapshot(SafetyState.Unsafe, "Cloud detection not implemented yet"));

        string[] lines = File.ReadAllLines(TodayFile());
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Local time,UTC time,Event,State,Safe", lines[0]);
        Assert.Contains(",Start,Unsafe,FALSE,Auto,,Cloud detection not implemented yet,", lines[1]);
        Assert.EndsWith(",1.0,Direct,0.0,7,2.0,,,,,,,,,,", lines[1]);
    }

    [Fact]
    public void MountAndRoiAreLoggedWithTheMountsOwnAltAzForDiagnostics()
    {
        var mount = new MountState(true, false, true, false, 20.142777, -89.5, CoordinateEpoch.JNow, 45.0, 0.0, clock.UtcNow);
        var roi = new RegionOfInterest(new HorizontalCoordinate(34.21, 180.5), 15, false, 0.97, string.Empty);
        Logger().Write(Snapshot(SafetyState.Unsafe, "x") with { Mount = mount, Roi = roi });

        string[] lines = File.ReadAllLines(TodayFile());
        Assert.EndsWith(",Mount,Mount RA (h),Mount Dec (°),Mount reported Alt (°),Mount reported Az (°),ROI Alt (°),ROI Az (°),ROI mode,ROI inside (%),ROI note", lines[0]);
        Assert.EndsWith(",Tracking,20.1428,-89.500,45.00,0.00,34.21,180.50,Circle,97,", lines[1]);
    }

    [Fact]
    public void WholeWorkingAreaFallbackIsLoggedWithItsReason()
    {
        var mount = new MountState(true, true, false, false, null, null, CoordinateEpoch.JNow, null, null, clock.UtcNow);
        var roi = new RegionOfInterest(null, 15, true, 0, "Mount parked: judging the whole working area");
        Logger().Write(Snapshot(SafetyState.Unsafe, "x") with { Mount = mount, Roi = roi });

        Assert.EndsWith(",Parked,,,,,,,Whole working area,,Mount parked: judging the whole working area", File.ReadAllLines(TodayFile())[1]);
    }

    [Fact]
    public void StateChangesAreMarked()
    {
        DecisionLogger log = Logger();
        log.Write(Snapshot(SafetyState.Unsafe, "x"));
        log.Write(Snapshot(SafetyState.Unsafe, "x"));
        log.Write(Snapshot(SafetyState.Safe, "All conditions clear"));

        string[] lines = File.ReadAllLines(TodayFile());
        Assert.Contains(",Start,", lines[1]);
        Assert.Contains(",,Unsafe,", lines[2]);
        Assert.Contains(",State change,Safe,TRUE,", lines[3]);
    }

    [Fact]
    public void ReasonsWithCommasAreQuotedAndSecretsRedacted()
    {
        Logger("abc123secret").Write(Snapshot(SafetyState.Unsafe, "Could not reach api, key abc123secret"));
        string row = File.ReadAllLines(TodayFile())[1];
        Assert.Contains("\"Could not reach api, key ***\"", row);
        Assert.DoesNotContain("abc123secret", row);
    }

    [Fact]
    public void FilesOlderThanRetentionAreDeleted()
    {
        Directory.CreateDirectory(folder);
        DateTime today = clock.UtcNow.ToLocalTime().Date;
        string old = Path.Combine(folder, DecisionLogger.FileName(today.AddDays(-7)));
        string kept = Path.Combine(folder, DecisionLogger.FileName(today.AddDays(-6)));
        File.WriteAllText(old, "x");
        File.WriteAllText(kept, "x");

        Logger().Write(Snapshot(SafetyState.Unsafe, "x"));

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(kept));
        Assert.Contains(sink.Entries, e => e.Message.StartsWith("Deleted old decision log decisions-", StringComparison.Ordinal));
    }

    [Fact]
    public void UnwritableFolderDoesNotThrow()
    {
        settings.Folder = Path.Combine(folder, "file-not-folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(settings.Folder, "x");
        Logger().Write(Snapshot(SafetyState.Unsafe, "x"));
    }
}
