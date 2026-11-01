using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Monitoring cycle whose behaviour the test supplies.</summary>
public sealed class DelegateCycle : IMonitoringCycle
{
    private readonly Func<CancellationToken, Task<MonitorSnapshot>> run;

    public DelegateCycle(Func<CancellationToken, Task<MonitorSnapshot>> run) => this.run = run;

    public int Runs { get; private set; }

    public Task<MonitorSnapshot> RunOnceAsync(CancellationToken ct)
    {
        Runs++;
        return run(ct);
    }

    public static MonitorSnapshot Snapshot(SafetyState state, bool safe, string reason) =>
        new(null, null, null, null, new SafetyConditions(Array.Empty<ConditionCheck>()),
            new SafetyStatus(state, safe, reason, null, OverrideMode.Auto, false, DateTime.UtcNow));
}
