using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-02", "REQ-09", "REQ-10")]
public sealed class MonitorServiceBuilderTests
{
    [Fact]
    public async Task BuiltServiceReportsIdleInDaytime()
    {
        var settings = new MonitorSettings();
        settings.ImageSource.IntervalS = 10;
        var ports = new MonitorPorts(new FakeClock(DateTime.UtcNow), new FakeSiteLocationSource(),
            new FakeAstroCalculator { SunAltitude = 30 }, new ListLogSink());

        MonitorService service = new MonitorServiceBuilder().Build(() => settings, ports);
        var first = new TaskCompletionSource<MonitorSnapshot>();
        service.SnapshotAvailable += (_, s) => first.TrySetResult(s);

        service.Start();
        MonitorSnapshot snapshot = await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync();

        Assert.Equal(SafetyState.Idle, snapshot.Status.State);
        Assert.False(snapshot.Status.IsSafe);
    }
}
