using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-10", "REQ-14", "REQ-32", "REQ-37")]
public sealed class MonitoringCycleTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly MonitorSettings settings = new();
    private readonly FakeAstroCalculator astro = new();
    private readonly FakeClock clock = new(Now);
    private readonly FakeImageSource direct = new(ImageSourceKind.Direct);
    private readonly FakeWeatherProvider weather = new();

    private MonitoringCycle Cycle() => new(
        () => settings,
        clock,
        new SiteLocationResolver(new FakeSiteLocationSource(), () => settings.Site),
        new OperatingWindow(astro),
        new ImageAcquirer(new ImageSourceFactory(new FakeImageSource(ImageSourceKind.Web), direct), new JpegIntegrityChecker(), clock, new ListLogSink(), () => settings.ImageSource, (_, _) => Task.CompletedTask),
        new ImageFreshnessEvaluator(),
        new WeatherPoller(weather, clock, new ListLogSink(), () => settings.Weather),
        new WeatherEvaluator());

    private static FetchResult FreshImage() =>
        FetchResult.New(new SkyImage(FakeImageSource.CompleteJpeg(), Now.AddMinutes(-1), ImageSourceKind.Direct));

    [Fact]
    public async Task IdleOutsideWindowWithoutFetchingAnything()
    {
        astro.SunAltitude = 10;
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Equal(SafetyState.Idle, s.Status.State);
        Assert.False(s.Status.IsSafe);
        Assert.Null(s.Image);
        Assert.Equal(0, direct.Fetches);
        Assert.Equal(0, weather.Requests);
    }

    [Fact]
    public async Task GoodImageAndWeatherLeaveOnlyCloudDetectionMissing()
    {
        astro.SunAltitude = -30;
        direct.Then(FreshImage());
        weather.Then(Now.AddMinutes(-2), 0, 8);
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Equal(SafetyState.Unsafe, s.Status.State);
        Assert.False(s.Status.IsSafe);
        Assert.NotNull(s.Image);
        Assert.NotNull(s.Weather);
        Assert.Equal("Cloud detection not implemented yet", s.Status.Reason);
        Assert.All(s.Conditions.Checks.Where(c => c.Condition != SafetyCondition.NoCloud), c => Assert.True(c.Passed));
    }

    [Fact]
    public async Task RainIsTheUnsafeReason()
    {
        astro.SunAltitude = -30;
        direct.Then(FreshImage());
        weather.Then(Now.AddMinutes(-1), 1.2, 8);
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.StartsWith("Rain 1.2 mm/h", s.Status.Reason);
    }

    [Fact]
    public async Task EveryFailedConditionIsInTheReason()
    {
        astro.SunAltitude = -30;
        direct.Then(FetchResult.Failed("Could not connect to allsky.local"));
        weather.Then(Now.AddMinutes(-1), 0, 55);
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Contains("Could not connect to allsky.local", s.Status.Reason);
        Assert.Contains("Gust 55 km/h (limit 40 km/h)", s.Status.Reason);
        Assert.Contains("Cloud detection not implemented yet", s.Status.Reason);
    }

    [Fact]
    public async Task NoWeatherDataIsReportedOnce()
    {
        astro.SunAltitude = -30;
        direct.Then(FreshImage());
        weather.Then(WeatherFetchResult.Failed("Weather API key was rejected (HTTP 401)"));
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Equal("No weather data: Weather API key was rejected (HTTP 401) · Cloud detection not implemented yet", s.Status.Reason);
        Assert.Equal(3, s.Conditions.Checks.Count(c => !c.Passed && c.Condition != SafetyCondition.NoCloud));
    }

    [Fact]
    public async Task WeatherCheckingOffIsNotPolledAndNotShown()
    {
        astro.SunAltitude = -30;
        settings.Weather.Enabled = false;
        direct.Then(FreshImage());
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, weather.Requests);
        Assert.Null(s.Weather);
        Assert.Equal("Cloud detection not implemented yet", s.Status.Reason);
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
