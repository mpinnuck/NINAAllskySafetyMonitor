using System.Reflection;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Profile.Interfaces;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>
/// The safety monitor device NINA lists under Equipment → Safety Monitor. Connect starts the
/// monitor, Disconnect stops it, and <see cref="IsSafe"/> reports the current decision.
/// </summary>
[Requirement("REQ-01", "REQ-02", "REQ-03", "REQ-49")]
public sealed class AllskySafetyMonitorDevice : BaseINPC, ISafetyMonitor
{
    private readonly IProfileService profileService;
    private readonly MonitorHost host = MonitorHost.Instance;
    private bool connected;

    public AllskySafetyMonitorDevice(IProfileService profileService)
    {
        this.profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        DriverVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0";
    }

    public string Id => "NINAAllskySafetyMonitor";

    public string Name => "Allsky Safety Monitor";

    public string DisplayName => Name;

    public string Category => "Allsky Safety Monitor";

    public string Description => "Cloud detection from Allsky camera images, with rain and wind from a weather station.";

    public string DriverInfo => "NINAAllskySafetyMonitor plugin";

    public string DriverVersion { get; }

    public bool HasSetupDialog => false;

    public IList<string> SupportedActions => new List<string>();

    public bool Connected
    {
        get => connected;
        private set
        {
            connected = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSafe));
        }
    }

    /// <summary>True only when connected and the monitor's current decision is safe.</summary>
    public bool IsSafe => Connected && host.CurrentStatus.IsSafe;

    public async Task<bool> Connect(CancellationToken token)
    {
        try
        {
            await host.StartAsync(profileService).ConfigureAwait(false);
            host.SnapshotAvailable -= OnSnapshot;
            host.SnapshotAvailable += OnSnapshot;
            Connected = true;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error($"[AllskySafetyMonitor] Connect failed: {e}");
            Connected = false;
            return false;
        }
    }

    public void Disconnect()
    {
        host.SnapshotAvailable -= OnSnapshot;
        try
        {
            // Run on the thread pool so a caller on the UI thread cannot deadlock.
            Task.Run(() => host.StopAsync()).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Logger.Error($"[AllskySafetyMonitor] Disconnect failed: {e}");
        }

        Connected = false;
    }

    public void SetupDialog()
    {
    }

    public string Action(string actionName, string actionParameters) =>
        throw new NotSupportedException($"Action '{actionName}' is not supported.");

    public string SendCommandString(string command, bool raw = true) =>
        throw new NotSupportedException("Commands are not supported.");

    public bool SendCommandBool(string command, bool raw = true) =>
        throw new NotSupportedException("Commands are not supported.");

    public void SendCommandBlind(string command, bool raw = true) =>
        throw new NotSupportedException("Commands are not supported.");

    private void OnSnapshot(object? sender, MonitorSnapshot snapshot) => RaisePropertyChanged(nameof(IsSafe));
}
