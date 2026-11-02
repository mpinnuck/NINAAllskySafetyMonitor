using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-02", "REQ-09", "REQ-10", "REQ-14", "REQ-32", "REQ-44")]
public sealed class MonitorServiceBuilderTests : IDisposable
{
    private readonly string logFolder = Path.Combine(Path.GetTempPath(), "allsky-builder-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(logFolder)) Directory.Delete(logFolder, recursive: true);
    }

    private MonitorSettings Settings()
    {
        var s = new MonitorSettings();
        s.Logging.Folder = logFolder;
        return s;
    }

    private static MonitorPorts Ports(double sunAltitude) => new(
        new FakeClock(DateTime.UtcNow), new FakeSiteLocationSource(),
        new FakeAstroCalculator { SunAltitude = sunAltitude }, new ListLogSink(), new FakeCredentialStore());

    private static async Task<MonitorSnapshot> FirstSnapshot(MonitorService service)
    {
        var first = new TaskCompletionSource<MonitorSnapshot>();
        service.SnapshotAvailable += (_, s) => first.TrySetResult(s);
        service.Start();
        MonitorSnapshot snapshot = await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync();
        return snapshot;
    }

    [Fact]
    public async Task BuiltServiceReportsIdleInDaytime()
    {
        MonitorSettings settings = Settings();
        settings.ImageSource.IntervalS = 10;

        MonitorSnapshot snapshot = await FirstSnapshot(new MonitorServiceBuilder().Build(() => settings, Ports(30)));

        Assert.Equal(SafetyState.Idle, snapshot.Status.State);
        Assert.False(snapshot.Status.IsSafe);
    }

    [Fact]
    public async Task BuiltServiceFetchesImageAndWeatherAtNight()
    {
        MonitorSettings settings = Settings();
        var direct = new FakeImageSource(ImageSourceKind.Direct)
            .Then(FetchResult.New(new SkyImage(FakeImageSource.CompleteJpeg(), DateTime.UtcNow, ImageSourceKind.Direct)));
        var sources = new ImageSourceFactory(new FakeImageSource(ImageSourceKind.Web), direct);
        var weather = new FakeWeatherProvider().Then(DateTime.UtcNow, 0, 5);

        MonitorSnapshot snapshot = await FirstSnapshot(new MonitorServiceBuilder().Build(() => settings, Ports(-30), sources, weather));

        Assert.Equal(1, direct.Fetches);
        Assert.Equal(1, weather.Requests);
        Assert.NotNull(snapshot.Image);
        Assert.NotNull(snapshot.Weather);
        Assert.False(snapshot.Status.IsSafe);
        Assert.Single(Directory.GetFiles(logFolder, "decisions-*.csv"));
    }
}
