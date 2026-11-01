using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-10", "REQ-14", "REQ-37")]
public sealed class MonitoringCycleTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly MonitorSettings settings = new();
    private readonly FakeAstroCalculator astro = new();
    private readonly FakeClock clock = new(Now);
    private readonly FakeImageSource direct = new(ImageSourceKind.Direct);

    private MonitoringCycle Cycle() => new(
        () => settings,
        clock,
        new SiteLocationResolver(new FakeSiteLocationSource(), () => settings.Site),
        new OperatingWindow(astro),
        new ImageAcquirer(new ImageSourceFactory(new FakeImageSource(ImageSourceKind.Web), direct), new JpegIntegrityChecker(), clock, new ListLogSink(), () => settings.ImageSource, (_, _) => Task.CompletedTask),
        new ImageFreshnessEvaluator());

    private static FetchResult FreshImage() =>
        FetchResult.New(new SkyImage(FakeImageSource.CompleteJpeg(), Now.AddMinutes(-1), ImageSourceKind.Direct));

    [Fact]
    public async Task IdleOutsideWindowWithoutFetching()
    {
        astro.SunAltitude = 10;
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Equal(SafetyState.Idle, s.Status.State);
        Assert.False(s.Status.IsSafe);
        Assert.Null(s.Image);
        Assert.Equal(0, direct.Fetches);
    }

    [Fact]
    public async Task FreshImageIsInSnapshotButStillUnsafeUntilCloudDetection()
    {
        astro.SunAltitude = -30;
        direct.Then(FreshImage());
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Equal(SafetyState.Unsafe, s.Status.State);
        Assert.False(s.Status.IsSafe);
        Assert.NotNull(s.Image);
        Assert.Contains(s.Conditions.Checks, c => c.Condition == SafetyCondition.FreshImage && c.Passed);
        Assert.StartsWith("Cloud detection not implemented yet · Image 1.0 min old", s.Status.Reason);
    }

    [Fact]
    public async Task FetchFailureIsTheUnsafeReason()
    {
        astro.SunAltitude = -30;
        direct.Then(FetchResult.Failed("Could not connect to allsky.local"));
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Equal(SafetyState.Unsafe, s.Status.State);
        Assert.Contains("Could not connect to allsky.local", s.Status.Reason);
        Assert.Null(s.Image);
    }

    [Fact]
    public async Task StaleImageIsTheUnsafeReason()
    {
        astro.SunAltitude = -30;
        direct.Then(FetchResult.New(new SkyImage(FakeImageSource.CompleteJpeg(), Now.AddMinutes(-15), ImageSourceKind.Direct)));
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Contains("min old (limit 10 min)", s.Status.Reason);
    }

    [Fact]
    public async Task SettingsChangesApplyOnNextCycle()
    {
        astro.SunAltitude = 10;
        direct.Then(FreshImage());
        MonitoringCycle cycle = Cycle();
        Assert.Equal(SafetyState.Idle, (await cycle.RunOnceAsync(CancellationToken.None)).Status.State);

        settings.Window.SunAltitudeLimitDeg = 90;
        Assert.Equal(SafetyState.Unsafe, (await cycle.RunOnceAsync(CancellationToken.None)).Status.State);
    }
}
