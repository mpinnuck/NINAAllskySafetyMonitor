using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-02", "REQ-09", "REQ-10", "REQ-14")]
public sealed class MonitorServiceBuilderTests
{
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
        var settings = new MonitorSettings();
        settings.ImageSource.IntervalS = 10;

        MonitorSnapshot snapshot = await FirstSnapshot(new MonitorServiceBuilder().Build(() => settings, Ports(30)));

        Assert.Equal(SafetyState.Idle, snapshot.Status.State);
        Assert.False(snapshot.Status.IsSafe);
    }

    [Fact]
    public async Task BuiltServiceFetchesTheImageAtNight()
    {
        var settings = new MonitorSettings();
        var direct = new FakeImageSource(ImageSourceKind.Direct)
            .Then(FetchResult.New(new SkyImage(FakeImageSource.CompleteJpeg(), DateTime.UtcNow, ImageSourceKind.Direct)));
        var sources = new ImageSourceFactory(new FakeImageSource(ImageSourceKind.Web), direct);

        MonitorSnapshot snapshot = await FirstSnapshot(new MonitorServiceBuilder().Build(() => settings, Ports(-30), sources));

        Assert.Equal(1, direct.Fetches);
        Assert.NotNull(snapshot.Image);
        Assert.False(snapshot.Status.IsSafe);
    }
}
