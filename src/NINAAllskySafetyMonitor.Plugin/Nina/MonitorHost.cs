using NINA.Profile.Interfaces;
using NINAAllskySafetyMonitor.Astrometry;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>
/// Owns the running <see cref="MonitorService"/> and relays its snapshots to the device and the
/// dock panel. One shared instance, because NINA creates those parts independently.
/// </summary>
[Requirement("REQ-02", "REQ-03")]
public sealed class MonitorHost
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object sync = new();
    private MonitorService? service;
    private MonitorSnapshot? latest;

    private MonitorHost()
    {
    }

    /// <summary>The shared instance.</summary>
    public static MonitorHost Instance { get; } = new();

    /// <summary>Raised on a background thread after every monitoring cycle.</summary>
    public event EventHandler<MonitorSnapshot>? SnapshotAvailable;

    /// <summary>Raised when the monitor starts or stops.</summary>
    public event EventHandler? RunningChanged;

    /// <summary>True while the monitor is running (device connected).</summary>
    public bool IsRunning
    {
        get { lock (sync) return service is not null; }
    }

    /// <summary>Latest snapshot, or null before the first cycle.</summary>
    public MonitorSnapshot? LatestSnapshot
    {
        get { lock (sync) return latest; }
    }

    /// <summary>Current decision; unsafe when not running.</summary>
    public SafetyStatus CurrentStatus
    {
        get
        {
            lock (sync) return service?.CurrentStatus ?? SafetyStatus.Starting(DateTime.UtcNow) with { Reason = "Not connected" };
        }
    }

    /// <summary>Builds and starts the monitor. Does nothing if already running.</summary>
    public async Task StartAsync(IProfileService profileService)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsRunning) return;

            var ports = new MonitorPorts(new SystemClock(), new NinaSiteLocationSource(profileService), new NovasAstroCalculator(), new NinaLogSink(), new WindowsCredentialStore());
            MonitorService built = new MonitorServiceBuilder().Build(() => PluginSettingsStore.Instance.Current, ports);
            built.SnapshotAvailable += OnSnapshot;

            lock (sync) service = built;
            built.Start();
            NINA.Core.Utility.Logger.Info("[AllskySafetyMonitor] Monitor started");
        }
        finally
        {
            gate.Release();
        }

        RunningChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops the monitor. Does nothing if not running.</summary>
    public async Task StopAsync()
    {
        MonitorService? stopping;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (sync)
            {
                stopping = service;
                service = null;
                latest = null;
            }

            if (stopping is null) return;
            stopping.SnapshotAvailable -= OnSnapshot;
            await stopping.StopAsync().ConfigureAwait(false);
            NINA.Core.Utility.Logger.Info("[AllskySafetyMonitor] Monitor stopped");
        }
        finally
        {
            gate.Release();
        }

        RunningChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSnapshot(object? sender, MonitorSnapshot snapshot)
    {
        lock (sync) latest = snapshot;
        SnapshotAvailable?.Invoke(this, snapshot);
    }
}
