using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>
/// Runs the monitoring cycle repeatedly on a background task (never the caller's thread) until
/// stopped. Publishes each snapshot through <see cref="SnapshotAvailable"/>. Status is Starting
/// (unsafe) until the first cycle completes.
/// </summary>
[Requirement("REQ-02", "REQ-49")]
public sealed class MonitorService
{
    private readonly IMonitoringCycle cycle;
    private readonly FailSafeGuard guard;
    private readonly Func<TimeSpan> interval;
    private readonly ILogSink log;
    private readonly object sync = new();
    private CancellationTokenSource? cts;
    private Task? loop;
    private SafetyStatus currentStatus;

    public MonitorService(IMonitoringCycle cycle, FailSafeGuard guard, Func<TimeSpan> interval, IClock clock, ILogSink log)
    {
        this.cycle = cycle ?? throw new ArgumentNullException(nameof(cycle));
        this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
        this.interval = interval ?? throw new ArgumentNullException(nameof(interval));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        ArgumentNullException.ThrowIfNull(clock);
        currentStatus = SafetyStatus.Starting(clock.UtcNow);
    }

    /// <summary>Raised on the background thread after every cycle.</summary>
    public event EventHandler<MonitorSnapshot>? SnapshotAvailable;

    /// <summary>The latest decision.</summary>
    public SafetyStatus CurrentStatus
    {
        get { lock (sync) return currentStatus; }
    }

    /// <summary>True while the background loop is running.</summary>
    public bool IsRunning
    {
        get { lock (sync) return loop is { IsCompleted: false }; }
    }

    /// <summary>Starts the background loop. Does nothing if already running.</summary>
    public void Start()
    {
        lock (sync)
        {
            if (loop is { IsCompleted: false }) return;
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            loop = Task.Run(() => RunLoopAsync(token), token);
        }
    }

    /// <summary>Stops the loop and waits for the current cycle to finish.</summary>
    public async Task StopAsync()
    {
        Task? running;
        lock (sync)
        {
            running = loop;
            cts?.Cancel();
        }

        if (running is not null)
        {
            try
            {
                await running.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on stop.
            }
        }

        lock (sync)
        {
            cts?.Dispose();
            cts = null;
            loop = null;
        }
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            MonitorSnapshot snapshot = await guard.RunAsync(cycle, token).ConfigureAwait(false);
            SafetyStatus previous;
            lock (sync)
            {
                previous = currentStatus;
                currentStatus = snapshot.Status;
            }

            // Log changes of state, and of reason except while idle (the idle reason carries the Sun altitude, which changes every cycle).
            if (previous.State != snapshot.Status.State
                || (snapshot.Status.State != SafetyState.Idle && previous.Reason != snapshot.Status.Reason))
            {
                log.Write(LogLevel.Info, $"State {snapshot.Status.State} (safe: {snapshot.Status.IsSafe}): {snapshot.Status.Reason}");
            }

            Publish(snapshot);
            await Task.Delay(interval(), token).ConfigureAwait(false);
        }
    }

    private void Publish(MonitorSnapshot snapshot)
    {
        try
        {
            SnapshotAvailable?.Invoke(this, snapshot);
        }
        catch (Exception e)
        {
            log.Write(LogLevel.Error, $"Snapshot subscriber failed: {e}");
        }
    }
}
