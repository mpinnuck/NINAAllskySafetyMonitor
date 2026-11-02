using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

/// <summary>A mount change re-evaluates at once instead of waiting for the (long) cycle interval.</summary>
[Requirement("REQ-27")]
public sealed class MonitorServiceWakeTests
{
    private readonly ListLogSink log = new();
    private readonly FakeClock clock = new(DateTime.UtcNow);

    private static async Task<bool> WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 300 && !condition(); i++) await Task.Delay(10);
        return condition();
    }

    private MonitorService Service(DelegateCycle cycle, MountWatcher? watcher = null) =>
        new(cycle, new FailSafeGuard(clock, log, () => TimeSpan.FromSeconds(5)), () => TimeSpan.FromMinutes(10), clock, log, mountWatcher: watcher);

    private static DelegateCycle Cycle() => new(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Unsafe, false, "x")));

    [Fact]
    public async Task RequestEvaluationRunsTheNextCycleNow()
    {
        DelegateCycle cycle = Cycle();
        MonitorService service = Service(cycle);
        service.Start();
        Assert.True(await WaitFor(() => cycle.Runs >= 1));

        service.RequestEvaluation();
        Assert.True(await WaitFor(() => cycle.Runs >= 2));
        await service.StopAsync();
    }

    [Fact]
    public async Task MountChangeRunsTheNextCycleNowAndIsLogged()
    {
        var mount = new FakeMountStateSource { Current = FakeMountStateSource.Tracking(5, -40) };
        var watcher = new MountWatcher(mount, () => TimeSpan.FromMilliseconds(20), log);
        DelegateCycle cycle = Cycle();
        MonitorService service = Service(cycle, watcher);

        service.Start();
        Assert.True(await WaitFor(() => cycle.Runs >= 1));
        await Task.Delay(100);
        int before = cycle.Runs;

        mount.Current = mount.Current! with { Slewing = true };
        Assert.True(await WaitFor(() => cycle.Runs > before));
        await service.StopAsync();

        Assert.Contains(log.Entries, e => e.Message.Contains("Mount changed (slewing): re-evaluating now"));
    }
}
