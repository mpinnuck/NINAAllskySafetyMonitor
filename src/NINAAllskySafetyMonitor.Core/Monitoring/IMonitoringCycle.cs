using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>One pass of the monitor: gather inputs, decide, and return the result.</summary>
public interface IMonitoringCycle
{
    /// <summary>Runs one cycle. May throw; callers wrap it in <see cref="FailSafeGuard"/>.</summary>
    Task<MonitorSnapshot> RunOnceAsync(CancellationToken ct);
}
