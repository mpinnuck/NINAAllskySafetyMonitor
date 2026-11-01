using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-49", "REQ-50", "REQ-51")]
public sealed class FailSafeGuardTests
{
    private readonly ListLogSink log = new();

    private FailSafeGuard Guard(double timeoutSeconds = 5) =>
        new(new FakeClock(DateTime.UtcNow), log, () => TimeSpan.FromSeconds(timeoutSeconds));

    [Fact]
    public async Task PassesThroughASuccessfulSnapshot()
    {
        var cycle = new DelegateCycle(_ => Task.FromResult(DelegateCycle.Snapshot(SafetyState.Safe, true, "clear")));
        MonitorSnapshot s = await Guard().RunAsync(cycle, CancellationToken.None);
        Assert.True(s.Status.IsSafe);
    }

    [Fact]
    public async Task ExceptionBecomesUnsafeAndIsLogged()
    {
        var cycle = new DelegateCycle(_ => throw new IOException("socket closed"));
        MonitorSnapshot s = await Guard().RunAsync(cycle, CancellationToken.None);

        Assert.False(s.Status.IsSafe);
        Assert.Equal(SafetyState.Unsafe, s.Status.State);
        Assert.Contains("socket closed", s.Status.Reason);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task HangBecomesUnsafeAfterTimeout()
    {
        var cycle = new DelegateCycle(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return DelegateCycle.Snapshot(SafetyState.Safe, true, "never");
        });

        MonitorSnapshot s = await Guard(timeoutSeconds: 0.2).RunAsync(cycle, CancellationToken.None);

        Assert.False(s.Status.IsSafe);
        Assert.Contains("timed out", s.Status.Reason);
    }

    [Fact]
    public async Task CallerCancellationIsRethrown()
    {
        using var cts = new CancellationTokenSource();
        var cycle = new DelegateCycle(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return DelegateCycle.Snapshot(SafetyState.Safe, true, "never");
        });

        Task<MonitorSnapshot> run = Guard().RunAsync(cycle, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }
}
