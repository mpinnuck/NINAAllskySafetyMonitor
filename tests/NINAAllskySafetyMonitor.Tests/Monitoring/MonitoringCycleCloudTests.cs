using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

/// <summary>The cycle's cloud step (SD-03) on synthetic frames at the 20 Oct 2026 frame time.</summary>
[Requirement("REQ-28", "REQ-37", "REQ-50", "REQ-60")]
public sealed class MonitoringCycleCloudTests
{
    private static readonly DateTime Now = SyntheticSky.FrameUtc;
    private readonly MonitorSettings settings = new();
    private readonly FakeAstroCalculator astro = new();
    private readonly FakeClock clock = new(Now);
    private readonly FakeImageSource direct = new(ImageSourceKind.Direct);
    private readonly FakeWeatherProvider weather = new();
    private readonly FakeMountStateSource mount = new();
    private readonly FakeFrameDecoder decoder = new() { Frame = SyntheticCloudSky.Frame() };
    private readonly List<ClearSkyReference> saved = new();

    public MonitoringCycleCloudTests()
    {
        settings.Calibration.Lens = SyntheticCloudSky.Lens;
        settings.Cloud.DecidesSafety = true; // These tests verify cloud detection affects safety
        astro.SunAltitude = -30;
        astro.FixedHorizontal = SyntheticCloudSky.SouthHigh;
        mount.Current = FakeMountStateSource.Tracking(5, -40);
        weather.Then(Now.AddMinutes(-2), 0, 8);
    }

    private MonitoringCycle Cycle() => new(
        () => settings,
        clock,
        new SiteLocationResolver(new FakeSiteLocationSource(), () => settings.Site),
        new OperatingWindow(astro),
        new ImageAcquirer(new ImageSourceFactory(new FakeImageSource(ImageSourceKind.Web), direct), new JpegIntegrityChecker(), clock, new ListLogSink(), () => settings.ImageSource, (_, _) => Task.CompletedTask),
        new ImageFreshnessEvaluator(),
        new WeatherPoller(weather, clock, new ListLogSink(), () => settings.Weather),
        new WeatherEvaluator(),
        mount,
        new RoiCalculator(astro, new MountStateInterpreter()),
        new RoiBuilder(),
        decoder,
        new CloudDetector(() => settings.Cloud, StarCatalogue.LoadEmbedded()),
        astro,
        SaveReference);

    private void SaveReference(ClearSkyReference reference)
    {
        saved.Add(reference);
        settings.Calibration.ClearSkyReference = reference;
    }

    private void NewImage() => direct.Then(FetchResult.New(new SkyImage(FakeImageSource.CompleteJpeg(), Now.AddMinutes(-1), ImageSourceKind.Direct)));

    [Fact]
    public async Task ClearSkyPassesEveryCondition()
    {
        NewImage();
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.True(s.Conditions.AllPassed, s.Status.Reason);
        Assert.False(s.NotASkyFrame);
        Assert.NotNull(s.Cloud);
        Assert.False(s.Cloud!.CloudPresent);
        Assert.StartsWith("Clear: ", s.Conditions.Checks.Single(c => c.Condition == SafetyCondition.NoCloud).Reason);
    }

    [Fact]
    public async Task CloudInTheRoiIsTheUnsafeReason()
    {
        decoder.Frame = SyntheticCloudSky.Frame(cloudCentre: SyntheticCloudSky.SouthHigh);
        NewImage();
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.False(s.Conditions.AllPassed);
        Assert.StartsWith("Cloud: ", s.Status.Reason);
        Assert.False(s.Conditions.OnlyUndeterminedFailures); // detected, so never assumed safe
    }

    [Fact]
    public async Task NoticeImageIsUnsafeWithItsReason()
    {
        decoder.Frame = SyntheticCloudSky.NoticeImage();
        NewImage();
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Equal("Not a sky frame (Allsky notice image?)", s.Status.Reason);
        Assert.Null(s.Cloud);
        Assert.False(s.Conditions.Checks.Single(c => c.Condition == SafetyCondition.NoCloud).Undetermined);
        Assert.True(s.NotASkyFrame);
    }

    [Fact]
    public async Task NoticeImageFailsWhenCloudDetectionIsAdvisory()
    {
        settings.Cloud.DecidesSafety = false;
        decoder.Frame = SyntheticCloudSky.NoticeImage();
        NewImage();

        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        ConditionCheck noCloud = s.Conditions.Checks.Single(c => c.Condition == SafetyCondition.NoCloud);
        Assert.False(noCloud.Passed);
        Assert.False(noCloud.Undetermined);
        Assert.True(s.NotASkyFrame);
    }

    [Fact]
    public async Task CloudUnknownIsUndetermined()
    {
        settings.Cloud.MinExpectedStars = 500;
        settings.Cloud.MinStarFraction = 0.95;
        NewImage();
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.StartsWith("Cloud unknown: ", s.Status.Reason);
        Assert.True(s.Conditions.OnlyUndeterminedFailures);
    }

    [Fact]
    public async Task UndecodableImageIsUnsafe()
    {
        decoder.Frame = null;
        NewImage();
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);
        Assert.StartsWith("Image could not be decoded", s.Status.Reason);
    }

    [Fact]
    public async Task UncalibratedLensIsUnsafe()
    {
        settings.Calibration.Lens = null;
        NewImage();
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);
        Assert.Equal("Lens not calibrated: calibrate it in the plugin options", s.Status.Reason);
    }

    [Fact]
    public async Task SameImageIsDecodedOnce()
    {
        NewImage();
        MonitoringCycle cycle = Cycle();
        await cycle.RunOnceAsync(CancellationToken.None);
        clock.UtcNow = Now.AddSeconds(5); // mount-triggered cycle before the image interval
        await cycle.RunOnceAsync(CancellationToken.None);
        Assert.Equal(1, decoder.Decodes);
    }

    [Fact]
    public async Task CapturedReferenceIsHandedOnAndUsed()
    {
        settings.Cloud.Tuned = true;
        NewImage();
        MonitoringCycle cycle = Cycle();
        MonitorSnapshot first = await cycle.RunOnceAsync(CancellationToken.None);
        Assert.Single(saved);
        Assert.True(first.Cloud!.BrightnessTestSkipped);

        clock.UtcNow = Now.AddSeconds(5);
        MonitorSnapshot second = await cycle.RunOnceAsync(CancellationToken.None);
        Assert.Single(saved);
        Assert.False(second.Cloud!.BrightnessTestSkipped);
    }

    [Fact]
    public async Task ClearingSavedReferenceStopsUsingTheCapturedReference()
    {
        settings.Cloud.Tuned = true;
        NewImage();
        MonitoringCycle cycle = Cycle();
        MonitorSnapshot first = await cycle.RunOnceAsync(CancellationToken.None);
        Assert.True(first.Cloud!.BrightnessTestSkipped);

        clock.UtcNow = Now.AddSeconds(5);
        MonitorSnapshot second = await cycle.RunOnceAsync(CancellationToken.None);
        Assert.False(second.Cloud!.BrightnessTestSkipped);

        settings.Calibration.ClearSkyReference = null;
        clock.UtcNow = Now.AddSeconds(10);
        MonitorSnapshot afterClear = await cycle.RunOnceAsync(CancellationToken.None);

        Assert.True(afterClear.Cloud!.BrightnessTestSkipped);
        Assert.Equal(2, saved.Count);
    }
}
