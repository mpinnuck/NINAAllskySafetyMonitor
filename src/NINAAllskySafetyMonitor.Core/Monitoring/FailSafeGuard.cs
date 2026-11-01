using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>
/// Runs a monitoring cycle with a timeout and a catch-all. Any exception or timeout becomes an
/// Unsafe snapshot; nothing propagates to the caller except cancellation of the caller's own token.
/// </summary>
[Requirement("REQ-49", "REQ-50", "REQ-51")]
public sealed class FailSafeGuard
{
    private readonly IClock clock;
    private readonly ILogSink log;
    private readonly Func<TimeSpan> timeout;

    public FailSafeGuard(IClock clock, ILogSink log, Func<TimeSpan> timeout)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.timeout = timeout ?? throw new ArgumentNullException(nameof(timeout));
    }

    /// <summary>Runs the cycle; returns its snapshot, or an Unsafe snapshot on failure.</summary>
    /// <exception cref="OperationCanceledException">Only when <paramref name="ct"/> itself is cancelled.</exception>
    public async Task<MonitorSnapshot> RunAsync(IMonitoringCycle cycle, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout());

        try
        {
            return await cycle.RunOnceAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            log.Write(LogLevel.Warning, $"Monitoring cycle timed out after {timeout().TotalSeconds:0} s");
            return Unsafe("Monitoring cycle timed out");
        }
        catch (Exception e)
        {
            log.Write(LogLevel.Error, $"Monitoring cycle failed: {e}");
            return Unsafe($"Monitor error: {e.Message}");
        }
    }

    private MonitorSnapshot Unsafe(string reason)
    {
        var status = new SafetyStatus(SafetyState.Unsafe, false, reason, null, OverrideMode.Auto, false, clock.UtcNow);
        return new MonitorSnapshot(null, null, null, null, new SafetyConditions(Array.Empty<ConditionCheck>()), status);
    }
}
