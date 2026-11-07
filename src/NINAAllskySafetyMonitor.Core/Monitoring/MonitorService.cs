using System.Diagnostics;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Decision;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>
/// Runs the monitoring cycle repeatedly on a background task (never the caller's thread) until
/// stopped. Each cycle's snapshot goes through the decision engine (immediate unsafe, delayed safe) and
/// the manual override, then is published through <see cref="SnapshotAvailable"/>. Status is Starting
/// (unsafe) until the first cycle completes. A manual override takes effect at once, not at the next cycle.
/// A change of mount state, or <see cref="RequestEvaluation"/>, runs the next cycle at once instead of
/// waiting for the interval. A cycle that exceeds the guard timeout is reported unsafe; the expected
/// wait between cycles does not count against liveness.
/// </summary>
[Requirement("REQ-02", "REQ-27", "REQ-38", "REQ-40", "REQ-49", "REQ-64")]
public sealed class MonitorService
{
    private readonly IMonitoringCycle cycle;
    private readonly FailSafeGuard guard;
    private readonly Func<TimeSpan> interval;
    private readonly ILogSink log;
    private readonly SafetyDecisionEngine? engine;
    private readonly ManualOverride? manualOverride;
    private readonly MountWatcher? mountWatcher;
    private readonly SemaphoreSlim wake = new(0, 1);
    private readonly object sync = new();
    private readonly IClock clock;
    private CancellationTokenSource? cts;
    private Task? loop;
    private SafetyStatus decidedStatus;
    private long cycleStartedTimestamp;
    private bool cycleInProgress;
    private bool livenessFailureLogged;
    private bool hasStarted;
    private bool stopRequested;

    public MonitorService(
        IMonitoringCycle cycle,
        FailSafeGuard guard,
        Func<TimeSpan> interval,
        IClock clock,
        ILogSink log,
        SafetyDecisionEngine? engine = null,
        ManualOverride? manualOverride = null,
        MountWatcher? mountWatcher = null)
    {
        this.cycle = cycle ?? throw new ArgumentNullException(nameof(cycle));
        this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
        this.interval = interval ?? throw new ArgumentNullException(nameof(interval));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.engine = engine;
        this.manualOverride = manualOverride;
        this.mountWatcher = mountWatcher;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (mountWatcher is not null) mountWatcher.Changed += OnMountChanged;
        decidedStatus = SafetyStatus.Starting(clock.UtcNow);
    }

    /// <summary>Raised on the background thread after every cycle, with the final (decided, overridden) status.</summary>
    public event EventHandler<MonitorSnapshot>? SnapshotAvailable;

    /// <summary>The latest decision, with the current manual override applied.</summary>
    public SafetyStatus CurrentStatus
    {
        get
        {
            SafetyStatus s;
            bool running;
            bool started;
            bool stopping;
            long startedAt;
            bool inProgress;
            lock (sync)
            {
                s = decidedStatus;
                running = loop is { IsCompleted: false };
                started = hasStarted;
                stopping = stopRequested;
                startedAt = cycleStartedTimestamp;
                inProgress = cycleInProgress;
            }

            if (started && stopping) return UnsafeStatus("Monitoring is stopping");

            if (started && !running)
            {
                string reason = s.State == SafetyState.Unsafe && !string.IsNullOrWhiteSpace(s.Reason)
                    ? $"Monitoring loop is not running: {s.Reason}"
                    : "Monitoring loop is not running";
                return UnsafeStatus(reason);
            }

            if (running && inProgress && startedAt != 0)
            {
                try
                {
                    TimeSpan deadline = guard.Timeout;
                    if (deadline < TimeSpan.Zero) deadline = TimeSpan.Zero;
                    if (Stopwatch.GetElapsedTime(startedAt) > deadline)
                    {
                        bool logFailure;
                        lock (sync)
                        {
                            logFailure = !livenessFailureLogged;
                            livenessFailureLogged = true;
                        }

                        if (logFailure)
                            log.Write(LogLevel.Error, $"No monitoring cycle completed within {deadline.TotalSeconds:0} s of its start");
                        return UnsafeStatus($"No monitoring cycle completed within {deadline.TotalSeconds:0} s");
                    }
                }
                catch (Exception e)
                {
                    log.Write(LogLevel.Error, $"Could not check monitoring liveness: {e}");
                    return UnsafeStatus("Monitoring liveness could not be checked");
                }
            }

            return manualOverride?.Apply(s) ?? s;
        }
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
            cts?.Dispose();
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            hasStarted = true;
            stopRequested = false;
            decidedStatus = SafetyStatus.Starting(clock.UtcNow);
            cycleStartedTimestamp = 0;
            cycleInProgress = false;
            livenessFailureLogged = false;
            loop = Task.Run(() => RunLoopAsync(token), token);
        }

        mountWatcher?.Start();
    }

    /// <summary>Runs the next cycle now instead of waiting for the interval (at most one request is queued).</summary>
    public void RequestEvaluation()
    {
        try
        {
            wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // A request is already waiting.
        }
    }

    /// <summary>Stops the loop and waits for the current cycle to finish.</summary>
    public async Task StopAsync()
    {
        Task? running;
        lock (sync)
        {
            stopRequested = true;
            running = loop;
            cts?.Cancel();
        }

        Task watcherStop = mountWatcher?.StopAsync() ?? Task.CompletedTask;
        Task loopStop = running ?? Task.CompletedTask;
        try
        {
            await Task.WhenAll(watcherStop, loopStop).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            log.Write(LogLevel.Warning, "Monitoring did not stop within 5 s; it remains unsafe and cannot be restarted until the current operation finishes");
            if (running is not null) _ = running.ContinueWith(_ => CleanupStoppedLoop(running), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return;
        }
        catch (OperationCanceledException)
        {
            // Expected on stop.
        }

        lock (sync)
        {
            cts?.Dispose();
            cts = null;
            loop = null;
            cycleInProgress = false;
            cycleStartedTimestamp = 0;
            hasStarted = false;
            stopRequested = false;
            decidedStatus = SafetyStatus.Starting(clock.UtcNow);
        }
    }

    private void CleanupStoppedLoop(Task stopped)
    {
        lock (sync)
        {
            if (loop != stopped || cts?.IsCancellationRequested != true) return;
            cts.Dispose();
            cts = null;
            loop = null;
            cycleInProgress = false;
            cycleStartedTimestamp = 0;
            hasStarted = false;
            stopRequested = false;
            decidedStatus = SafetyStatus.Starting(clock.UtcNow);
        }
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                lock (sync)
                {
                    cycleStartedTimestamp = Stopwatch.GetTimestamp();
                    cycleInProgress = true;
                    livenessFailureLogged = false;
                }

                MonitorSnapshot snapshot = await guard.RunAsync(cycle, token).ConfigureAwait(false);
                SafetyStatus decided = engine?.Decide(snapshot) ?? snapshot.Status;

                SafetyStatus previous;
                lock (sync)
                {
                    previous = decidedStatus;
                    decidedStatus = decided;
                    cycleInProgress = false;
                    cycleStartedTimestamp = 0;
                    livenessFailureLogged = false;
                }

                // Log changes of state or of the kind of reason. Numbers (image age, Sun altitude, countdown)
                // change every cycle, so they are ignored when comparing reasons.
                if (previous.State != decided.State || ReasonKind(previous.Reason) != ReasonKind(decided.Reason))
                {
                    log.Write(LogLevel.Info, $"State {decided.State} (safe: {decided.IsSafe}): {decided.Reason}");
                }

                SafetyStatus reported = manualOverride?.Apply(decided) ?? decided;
                Publish(snapshot with { Status = reported });
                await wake.WaitAsync(interval(), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            log.Write(LogLevel.Error, $"Monitoring loop failed: {e}");
            SafetyStatus failed = UnsafeStatus($"Monitoring loop failed: {e.Message}");
            lock (sync)
            {
                decidedStatus = failed;
                cycleInProgress = false;
                cycleStartedTimestamp = 0;
            }

            Publish(new MonitorSnapshot(null, null, null, null,
                new SafetyConditions(Array.Empty<ConditionCheck>()), failed));
        }
    }

    private SafetyStatus UnsafeStatus(string reason) =>
        new(SafetyState.Unsafe, false, reason, null, OverrideMode.Auto, false, clock.UtcNow);

    private void OnMountChanged(object? sender, MountState? mount)
    {
        string what = mount is null ? "no information" : DescribeMount(mount);
        log.Write(LogLevel.Info, $"Mount changed ({what}): re-evaluating now");
        RequestEvaluation();
    }

    private static string DescribeMount(MountState m) =>
        !m.Connected ? "not connected" : m.AtPark ? "parked" : m.Slewing ? "slewing" : m.Tracking ? "tracking" : "not tracking";

    /// <summary>The reason with its numbers removed, e.g. "Image # min old".</summary>
    internal static string ReasonKind(string reason) =>
        System.Text.RegularExpressions.Regex.Replace(reason ?? string.Empty, @"[-+−]?\d+(\.\d+)?", "#");

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
