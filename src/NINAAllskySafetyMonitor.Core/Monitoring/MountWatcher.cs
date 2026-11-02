using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>
/// Reads the mount state at its own short interval, independent of the image cycle, and raises
/// <see cref="Changed"/> when something that matters changes: connected, parked, tracking or slewing,
/// whether there is a position, or a move of more than <see cref="MoveThresholdDeg"/>. The monitor then
/// re-evaluates at once, so a slew is held as soon as it starts and the ROI moves as soon as it ends.
/// </summary>
[Requirement("REQ-05", "REQ-27")]
public sealed class MountWatcher
{
    /// <summary>A position change smaller than this (tracking, small dithers) is not a change.</summary>
    public const double MoveThresholdDeg = 0.5;

    private readonly IMountStateSource source;
    private readonly Func<TimeSpan> pollInterval;
    private readonly ILogSink log;
    private readonly object sync = new();
    private CancellationTokenSource? cts;
    private Task? loop;
    private bool hasPolled;
    private MountState? last;
    private bool readFailureLogged;

    public MountWatcher(IMountStateSource source, Func<TimeSpan> pollInterval, ILogSink log)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.pollInterval = pollInterval ?? throw new ArgumentNullException(nameof(pollInterval));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Raised on the watcher's thread with the new state when the mount changes significantly.</summary>
    public event EventHandler<MountState?>? Changed;

    /// <summary>Reads the mount once. The first read sets the baseline; later reads raise <see cref="Changed"/> on a change.</summary>
    /// <returns>True when a change was raised.</returns>
    public bool Poll()
    {
        MountState? now = source.GetCurrent();
        bool changed = hasPolled && Significant(last, now);
        hasPolled = true;
        last = now;
        if (changed) Changed?.Invoke(this, now);
        return changed;
    }

    /// <summary>Starts polling on a background task. Does nothing if already running.</summary>
    public void Start()
    {
        lock (sync)
        {
            if (loop is { IsCompleted: false }) return;
            hasPolled = false;
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            loop = Task.Run(() => RunAsync(token), token);
        }
    }

    /// <summary>Stops polling and waits for the loop to end.</summary>
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

    /// <summary>True when the difference between two mount states should trigger a re-evaluation.</summary>
    internal static bool Significant(MountState? a, MountState? b)
    {
        if (a is null || b is null) return a is null != b is null;
        if (a.Connected != b.Connected || a.AtPark != b.AtPark || a.Tracking != b.Tracking || a.Slewing != b.Slewing) return true;
        if (a.HasValidPosition != b.HasValidPosition) return true;
        if (!a.HasValidPosition) return false;

        // RA/Dec treated as longitude/latitude on the sphere.
        var pa = new HorizontalCoordinate(a.DecDeg!.Value, a.RaHours!.Value * 15.0);
        var pb = new HorizontalCoordinate(b.DecDeg!.Value, b.RaHours!.Value * 15.0);
        return SkyGeometry.SeparationDeg(pa, pb) > MoveThresholdDeg;
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                Poll();
                readFailureLogged = false;
            }
            catch (Exception e)
            {
                if (!readFailureLogged)
                {
                    log.Write(LogLevel.Warning, $"Mount state could not be read: {e.Message}");
                    readFailureLogged = true;
                }
            }

            await Task.Delay(pollInterval(), token).ConfigureAwait(false);
        }
    }
}
