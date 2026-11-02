using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-02", "REQ-49")]
public sealed class MonitorServiceTests
{
    private readonly ListLogSink log = new();
    private readonly FakeClock clock = new(DateTime.UtcNow);

    private MonitorService Service(IMonitoringCycle cycle) =>
        new(cycle, new FailSafeGuard(clock, log, () => TimeSpan.FromSeconds(5)), () => TimeSpan.FromMilliseconds(20), clock, log);

    private static async Task<bool> WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        return condition();
    }

    [Fact]
    public void StartsInStartingStateWhichIsUnsafe()
    {
        MonitorService service = Service(new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Safe, true, "x"))));
        Assert.Equal(SafetyState.Starting, service.CurrentStatus.State);
        Assert.False(service.CurrentStatus.IsSafe);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task RunsCyclesAndPublishesSnapshotsUntilStopped()
    {
        var cycle = new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Idle, false, "daytime")));
        MonitorService service = Service(cycle);
        int published = 0;
        service.SnapshotAvailable += (_, _) => Interlocked.Increment(ref published);

        service.Start();
        Assert.True(await WaitFor(() => Volatile.Read(ref published) >= 2));
        Assert.Equal(SafetyState.Idle, service.CurrentStatus.State);

        await service.StopAsync();
        Assert.False(service.IsRunning);
        int afterStop = Volatile.Read(ref published);
        await Task.Delay(100);
        Assert.Equal(afterStop, Volatile.Read(ref published));
    }

    [Fact]
    public async Task FailingSubscriberDoesNotStopTheLoop()
    {
        var cycle = new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Idle, false, "daytime")));
        MonitorService service = Service(cycle);
        service.SnapshotAvailable += (_, _) => throw new InvalidOperationException("UI gone");

        service.Start();
        Assert.True(await WaitFor(() => cycle.Runs >= 3));
        Assert.True(service.IsRunning);
        await service.StopAsync();
    }

    [Theory]
    [InlineData("Cloud detection not implemented yet · Image 0.3 min old", "Cloud detection not implemented yet · Image 0.6 min old")]
    [InlineData("Outside operating window: Sun at −13.4° (limit −18°)", "Outside operating window: Sun at −15.9° (limit −18°)")]
    public void ReasonsDifferingOnlyInNumbersAreTheSameKind(string a, string b) =>
        Assert.Equal(MonitorService.ReasonKind(a), MonitorService.ReasonKind(b));

    [Fact]
    public void DifferentReasonsAreDifferentKinds() =>
        Assert.NotEqual(
            MonitorService.ReasonKind("Cloud detection not implemented yet · Image 0.3 min old"),
            MonitorService.ReasonKind("Image is 12.0 min old (limit 10 min)"));

    [Fact]
    public async Task StopWithoutStartIsHarmless() =>
        await Service(new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Idle, false, "x")))).StopAsync();
}
