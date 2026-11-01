using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Plugin.Nina;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// Dock panel in the Imaging tab showing the monitor's state, reason and the latest Allsky image.
/// Later slices add the ROI overlay, metrics and history graph.
/// </summary>
[Export(typeof(IDockableVM))]
[Requirement("REQ-43", "REQ-45")]
public sealed class StatusViewModel : DockableVM
{
    private readonly MonitorHost host = MonitorHost.Instance;
    private readonly DispatcherTimer refreshTimer;
    private SkyImage? shownImage;
    private BitmapSource? image;

    [ImportingConstructor]
    public StatusViewModel(IProfileService profileService) : base(profileService)
    {
        Title = "Allsky Safety";
        host.SnapshotAvailable += (_, _) => OnUiThread(Refresh);
        host.RunningChanged += (_, _) => OnUiThread(Refresh);

        // Safety net: also refresh every few seconds, so the panel can never stay stale if an event is
        // missed, and so the image age keeps counting.
        refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher) { Interval = TimeSpan.FromSeconds(5) };
        refreshTimer.Tick += (_, _) => Refresh();
        refreshTimer.Start();
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

    /// <summary>The latest Allsky image, or null when there is none (idle, not connected or not fetched yet).</summary>
    public BitmapSource? AllskyImage => image;

    public bool HasImage => image is not null;

    /// <summary>Capture time, age and source of the image shown.</summary>
    public string ImageInfoText
    {
        get
        {
            SkyImage? img = shownImage;
            if (img is null) return string.Empty;
            double age = Math.Max(0, (DateTime.UtcNow - img.CaptureTimeUtc).TotalMinutes);
            return string.Create(
                CultureInfo.CurrentCulture,
                $"Image captured {img.CaptureTimeUtc.ToLocalTime():HH:mm:ss} ({age:0.0} min ago) · {img.Source} · {img.JpegBytes.Length / 1024} KB");
        }
    }

    private void Refresh()
    {
        SkyImage? current = host.IsRunning ? host.LatestSnapshot?.Image : null;
        if (!ReferenceEquals(current, shownImage))
        {
            shownImage = current;
            image = current is null ? null : Decode(current.JpegBytes);
            RaisePropertyChanged(nameof(AllskyImage));
            RaisePropertyChanged(nameof(HasImage));
        }

        RaisePropertyChanged(nameof(IsConnected));
        RaisePropertyChanged(nameof(IsSafe));
        RaisePropertyChanged(nameof(StateText));
        RaisePropertyChanged(nameof(ReasonText));
        RaisePropertyChanged(nameof(LastUpdateText));
        RaisePropertyChanged(nameof(UntunedVisible));
        RaisePropertyChanged(nameof(ImageInfoText));
    }

    /// <summary>Decodes at panel resolution; a JPEG that can't be decoded is simply not shown.</summary>
    private static BitmapSource? Decode(byte[] jpeg)
    {
        try
        {
            using var stream = new MemoryStream(jpeg);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 800;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
