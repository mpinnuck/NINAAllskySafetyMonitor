using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Decision;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-02", "REQ-38", "REQ-40", "REQ-49", "REQ-64")]
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
    public async Task DecisionEngineDecidesThePublishedStatus()
    {
        var cycle = new DelegateCycle(_ => Task.FromResult(AllClear()));
        var engine = new SafetyDecisionEngine(clock, () => TimeSpan.FromMinutes(4));
        var service = new MonitorService(cycle, new FailSafeGuard(clock, log, () => TimeSpan.FromSeconds(5)), () => TimeSpan.FromMilliseconds(20), clock, log, engine);
        MonitorSnapshot? published = null;
        service.SnapshotAvailable += (_, s) => published = s;

        service.Start();
        Assert.True(await WaitFor(() => published is not null));
        SafetyStatus status = service.CurrentStatus;
        await service.StopAsync();

        Assert.False(status.IsSafe);
        Assert.Equal(240, status.SecondsToSafe);
        Assert.Equal(240, published!.Status.SecondsToSafe);
    }

    [Fact]
    public async Task HungCycleIsReportedUnsafeAfterLivenessDeadline()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int runs = 0;
        var cycle = new DelegateCycle(async _ =>
        {
            if (Interlocked.Increment(ref runs) == 1)
                return DelegateCycle.Snapshot(SafetyState.Safe, true, "previously safe");
            started.TrySetResult();
            await release.Task;
            return DelegateCycle.Snapshot(SafetyState.Safe, true, "previously safe");
        });
        var service = new MonitorService(
            cycle,
            new FailSafeGuard(clock, log, () => TimeSpan.FromMilliseconds(50)),
            () => TimeSpan.FromHours(1),
            clock,
            log);

        service.Start();
        Assert.True(await WaitFor(() => service.CurrentStatus.IsSafe));
        service.RequestEvaluation();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        Assert.False(service.CurrentStatus.IsSafe);
        Assert.Contains("No monitoring cycle completed", service.CurrentStatus.Reason);
        Assert.Contains(log.Entries, entry => entry.Message.Contains("No monitoring cycle completed"));

        Task stopping = service.StopAsync();
        Assert.False(service.CurrentStatus.IsSafe);
        Assert.Equal("Monitoring is stopping", service.CurrentStatus.Reason);
        release.TrySetResult();
        await stopping;
    }

    [Fact]
    public async Task BlockingSnapshotSubscriberIsReportedUnsafeAfterLivenessDeadline()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new MonitorService(
            new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Safe, true, "clear"))),
            new FailSafeGuard(clock, log, () => TimeSpan.FromMilliseconds(50)),
            () => TimeSpan.FromHours(1),
            clock,
            log);
        int published = 0;
        service.SnapshotAvailable += (_, _) =>
        {
            if (Interlocked.Increment(ref published) == 2)
            {
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            }
        };

        service.Start();
        Assert.True(await WaitFor(() => service.CurrentStatus.IsSafe));
        service.RequestEvaluation();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        Assert.False(service.CurrentStatus.IsSafe);
        Assert.Contains("No monitoring cycle completed", service.CurrentStatus.Reason);

        release.TrySetResult();
        await service.StopAsync();
    }

    [Fact]
    public async Task BlockingLogSinkIsReportedUnsafeAfterLivenessDeadline()
    {
        var blockingLog = new BlockingLogSink();
        var service = new MonitorService(
            new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Safe, true, "clear"))),
            new FailSafeGuard(clock, blockingLog, () => TimeSpan.FromMilliseconds(50)),
            () => TimeSpan.FromHours(1),
            clock,
            blockingLog);

        service.Start();
        await blockingLog.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        Assert.False(service.CurrentStatus.IsSafe);
        Assert.Contains("No monitoring cycle completed", service.CurrentStatus.Reason);

        blockingLog.Release.TrySetResult();
        await service.StopAsync();
    }

    [Fact]
    public async Task RestartResetsPreviousSafeStatusUntilANewCycleCompletes()
    {
        var restartedCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRestartedCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int runs = 0;
        var cycle = new DelegateCycle(async _ =>
        {
            if (Interlocked.Increment(ref runs) == 1)
                return DelegateCycle.Snapshot(SafetyState.Safe, true, "safe");
            restartedCycle.TrySetResult();
            await finishRestartedCycle.Task;
            return DelegateCycle.Snapshot(SafetyState.Safe, true, "safe");
        });
        var service = Service(cycle);

        service.Start();
        Assert.True(await WaitFor(() => service.CurrentStatus.IsSafe));
        await service.StopAsync();
        Assert.Equal(SafetyState.Starting, service.CurrentStatus.State);
        Assert.False(service.CurrentStatus.IsSafe);

        service.Start();
        await restartedCycle.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(SafetyState.Starting, service.CurrentStatus.State);
        Assert.False(service.CurrentStatus.IsSafe);

        finishRestartedCycle.TrySetResult();
        Assert.True(await WaitFor(() => service.CurrentStatus.IsSafe));
        await service.StopAsync();
    }

    [Fact]
    public async Task LoopFaultAfterSafeCycleChangesStatusToUnsafe()
    {
        var cycle = new DelegateCycle(_ =>
            Task.FromResult(DelegateCycle.Snapshot(SafetyState.Safe, true, "safe")));
        var service = new MonitorService(
            cycle,
            new FailSafeGuard(clock, log, () => TimeSpan.FromSeconds(5)),
            () => throw new InvalidOperationException("invalid interval"),
            clock,
            log);

        service.Start();
        Assert.True(await WaitFor(() => cycle.Runs == 1 && !service.IsRunning));

        Assert.False(service.CurrentStatus.IsSafe);
        Assert.Contains("Monitoring loop is not running", service.CurrentStatus.Reason);
        Assert.Contains(log.Entries, entry => entry.Message.Contains("Monitoring loop failed"));
        await service.StopAsync();
    }

    [Fact]
    public void ManualOverrideTakesEffectImmediately()
    {
        var manual = new ManualOverride(log);
        var service = new MonitorService(new DelegateCycle(_ => Task.FromResult(AllClear())), new FailSafeGuard(clock, log, () => TimeSpan.FromSeconds(5)), () => TimeSpan.FromSeconds(60), clock, log, null, manual);
        Assert.False(service.CurrentStatus.IsSafe);

        manual.Mode = OverrideMode.ForceSafe;
        Assert.True(service.CurrentStatus.IsSafe);

        manual.Mode = OverrideMode.Auto;
        Assert.False(service.CurrentStatus.IsSafe);
    }

    [Fact]
    public async Task ForceSafeDoesNotApplyAfterMonitoringStops()
    {
        var manual = new ManualOverride(log) { Mode = OverrideMode.ForceSafe };
        var service = new MonitorService(
            new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Safe, true, "clear"))),
            new FailSafeGuard(clock, log, () => TimeSpan.FromSeconds(5)),
            () => TimeSpan.FromHours(1),
            clock,
            log,
            manualOverride: manual);

        Assert.True(service.CurrentStatus.IsSafe);
        service.Start();
        Assert.True(await WaitFor(() => service.CurrentStatus.IsSafe));
        await service.StopAsync();

        Assert.Equal(SafetyState.Starting, service.CurrentStatus.State);
        Assert.False(service.CurrentStatus.IsSafe);
    }

    private static MonitorSnapshot AllClear() =>
        new(null, null, null, null,
            new SafetyConditions(new[] { new ConditionCheck(SafetyCondition.ActiveWindow, true, "ok"), new ConditionCheck(SafetyCondition.NoCloud, true, "ok") }),
            new SafetyStatus(SafetyState.Unsafe, false, string.Empty, null, OverrideMode.Auto, false, DateTime.UtcNow));

    private sealed class BlockingLogSink : ILogSink
    {
        private int blocked;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Write(LogLevel level, string message)
        {
            if (level != LogLevel.Info || !message.StartsWith("State Safe", StringComparison.Ordinal)
                || Interlocked.Exchange(ref blocked, 1) != 0)
                return;

            Entered.TrySetResult();
            Release.Task.GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task StopWithoutStartIsHarmless() =>
        await Service(new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Idle, false, "x")))).StopAsync();
}
