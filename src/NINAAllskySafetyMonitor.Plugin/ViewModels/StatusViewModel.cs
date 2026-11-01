using System.ComponentModel.Composition;
using System.Globalization;
using System.Windows;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Plugin.Nina;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// Dock panel in the Imaging tab showing the monitor's state and reason. Slice 1 shows the state,
/// reason and update time; later slices add the image, metrics and history graph.
/// </summary>
[Export(typeof(IDockableVM))]
[Requirement("REQ-43", "REQ-45")]
public sealed class StatusViewModel : DockableVM
{
    private readonly MonitorHost host = MonitorHost.Instance;

    [ImportingConstructor]
    public StatusViewModel(IProfileService profileService) : base(profileService)
    {
        Title = "Allsky Safety";
        host.SnapshotAvailable += (_, _) => OnUiThread(Refresh);
        host.RunningChanged += (_, _) => OnUiThread(Refresh);
    }

    public bool IsConnected => host.IsRunning;

    public bool IsSafe => host.IsRunning && host.CurrentStatus.IsSafe;

    public string StateText => host.IsRunning ? host.CurrentStatus.State.ToString().ToUpperInvariant() : "NOT CONNECTED";

    public string ReasonText => host.IsRunning
        ? host.CurrentStatus.Reason
        : "Connect Allsky Safety Monitor in Equipment → Safety Monitor.";

    public string LastUpdateText
    {
        get
        {
            MonitorSnapshot? s = host.LatestSnapshot;
            return s is null
                ? string.Empty
                : "Updated " + s.Status.TimestampUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        }
    }

    public bool UntunedVisible => host.IsRunning && host.CurrentStatus.Untuned;

    private void Refresh()
    {
        RaisePropertyChanged(nameof(IsConnected));
        RaisePropertyChanged(nameof(IsSafe));
        RaisePropertyChanged(nameof(StateText));
        RaisePropertyChanged(nameof(ReasonText));
        RaisePropertyChanged(nameof(LastUpdateText));
        RaisePropertyChanged(nameof(UntunedVisible));
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
