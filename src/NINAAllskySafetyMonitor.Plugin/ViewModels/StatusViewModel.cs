using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Plugin.Imaging;
using NINAAllskySafetyMonitor.Plugin.Nina;
using NINAAllskySafetyMonitor.Plugin.Views;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// Dock panel in the Imaging tab showing the monitor's state, reason, the latest Allsky image with the
/// working area, ROI and expected stars drawn on it, the mount position, the cloud metrics and tonight's
/// history graph.
/// </summary>
[Export(typeof(IDockableVM))]
[Requirement("REQ-43", "REQ-45", "REQ-50", "REQ-58", "REQ-59")]
public sealed class StatusViewModel : DockableVM
{
    private readonly MonitorHost host = MonitorHost.Instance;
    private readonly DispatcherTimer refreshTimer;
    private SkyImage? shownImage;
    private BitmapSource? decoded;
    private int originalWidth;
    private int originalHeight;
    private MonitorSnapshot? composedFor;
    private BitmapSource? image;
    private bool showOverlay = true;

    [ImportingConstructor]
    public StatusViewModel(IProfileService profileService) : base(profileService)
    {
        Title = "Allsky Safety";
        host.SnapshotAvailable += (_, _) => OnUiThread(Refresh);
        host.RunningChanged += (_, _) => OnUiThread(Refresh);
        host.Override.Changed += (_, _) => OnUiThread(Refresh);

        // Safety net: also refresh every few seconds, so the panel can never stay stale if an event is
        // missed, and so the image age keeps counting.
        refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher) { Interval = TimeSpan.FromSeconds(5) };
        refreshTimer.Tick += (_, _) => Refresh();
        refreshTimer.Start();
        OpenViewerCommand = new DelegateCommand(OpenViewer);
    }

    /// <summary>Cloud metrics behind the latest decision.</summary>
    public MetricsViewModel Metrics { get; } = new();

    /// <summary>Tonight's star fraction, relative brightness and safe state.</summary>
    public HistoryChartViewModel History { get; } = new();

    /// <summary>Opens the latest image enlarged (REQ-59).</summary>
    public ICommand OpenViewerCommand { get; }

    public bool IsConnected => host.IsRunning;

    public bool IsSafe => host.IsRunning && host.CurrentStatus.IsSafe;

    /// <summary>Safe only because undetermined conditions are assumed safe (REQ-50): shown in amber.</summary>
    public bool IsAssumed => IsSafe && host.CurrentStatus.Assumed;

    public string StateText => !host.IsRunning
        ? "NOT CONNECTED"
        : host.CurrentStatus.State.ToString().ToUpperInvariant() + (IsAssumed ? " (ASSUMED)" : string.Empty);

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

    // ---- Manual override (REQ-40) ----

    public IReadOnlyList<OverrideMode> OverrideModes { get; } = new[] { OverrideMode.Auto, OverrideMode.ForceSafe, OverrideMode.ForceUnsafe };

    public OverrideMode OverrideMode
    {
        get => host.Override.Mode;
        set => host.Override.Mode = value;
    }

    public bool OverrideActive => host.Override.Mode != OverrideMode.Auto;

    public string OverrideBanner => host.Override.Mode switch
    {
        OverrideMode.ForceSafe => "MANUAL OVERRIDE: FORCE SAFE. NINA is told it is safe, whatever the sky. Set Auto when testing is done.",
        OverrideMode.ForceUnsafe => "MANUAL OVERRIDE: FORCE UNSAFE. Set Auto when testing is done.",
        _ => string.Empty,
    };

    /// <summary>Time left until safe while every condition is clear (REQ-39), counting down between cycles.</summary>
    public string CountdownText
    {
        get
        {
            if (!host.IsRunning) return string.Empty;
            SafetyStatus s = host.CurrentStatus;
            if (s.SecondsToSafe is not int seconds) return string.Empty;
            int left = Math.Max(0, seconds - (int)(DateTime.UtcNow - s.TimestampUtc).TotalSeconds);
            return $"Safe in {left / 60}:{left % 60:00} if conditions stay clear";
        }
    }

    /// <summary>The latest Allsky image, or null when there is none (idle, not connected or not fetched yet).</summary>
    public BitmapSource? AllskyImage => image;

    public bool HasImage => image is not null;

    /// <summary>Draw the working area and ROI on the image (this NINA session only).</summary>
    public bool ShowOverlay
    {
        get => showOverlay;
        set
        {
            showOverlay = value;
            composedFor = null;
            Refresh();
        }
    }

    /// <summary>Where the ROI is, or why the whole working area is judged (REQ-26, REQ-58).</summary>
    public string RoiInfoText
    {
        get
        {
            RegionOfInterest? roi = host.IsRunning ? host.LatestSnapshot?.Roi : null;
            if (roi is null) return string.Empty;
            if (roi.UsesWholeWorkingArea) return "Judging the whole working area · " + roi.Note;
            HorizontalCoordinate c = roi.Centre!.Value;
            return string.Create(
                CultureInfo.CurrentCulture,
                $"ROI {roi.RadiusDeg:0}° circle at alt {c.AltDeg:0.0}° az {c.AzDeg:0.0}° · {roi.InsideFraction * 100:0}% inside the working area");
        }
    }

    /// <summary>Mount state and position from NINA. The mount's own Alt/Az are shown but not used (REQ-06).</summary>
    public string MountInfoText
    {
        get
        {
            if (!host.IsRunning) return string.Empty;
            MountState? m = host.LatestSnapshot?.Mount;
            if (m is null) return "Mount: no information from NINA";
            if (!m.Connected) return "Mount: not connected";
            string state = m.AtPark ? "parked" : m.Slewing ? "slewing" : m.Tracking ? "tracking" : "not tracking";
            if (!m.HasValidPosition) return $"Mount: {state}, position unavailable";
            string reported = m.ReportedAltDeg is double alt && m.ReportedAzDeg is double az
                ? string.Create(CultureInfo.CurrentCulture, $" · mount reports alt {alt:0.0}° az {az:0.0}° (not used)")
                : string.Empty;
            return $"Mount: {state} · RA {Hms(m.RaHours!.Value)} Dec {Dms(m.DecDeg!.Value)} ({m.Epoch}){reported}";
        }
    }

    /// <summary>What the overlay colours mean, and whether the lens model is approximate.</summary>
    public string OverlayLegendText
    {
        get
        {
            if (!showOverlay || image is null) return string.Empty;
            bool approximate = PluginSettingsStore.Instance.Current.Calibration.Lens is not { Manual: false };
            return "Yellow dashed: working area (green when the whole area is judged) · Blue: drawn outline · Green: ROI (orange dotted: target not used) · Small circles: expected stars, green found, red missing"
                + (approximate ? " · Positions on the image are approximate until the lens is calibrated" : string.Empty);
        }
    }

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

    /// <summary>Rain, gust and age of the latest weather observation, or empty when weather is off or not yet read.</summary>
    public string WeatherInfoText
    {
        get
        {
            WeatherObservation? w = host.IsRunning ? host.LatestSnapshot?.Weather : null;
            if (w is null) return string.Empty;
            double age = Math.Max(0, (DateTime.UtcNow - w.ObservationTimeUtc).TotalMinutes);
            return string.Create(
                CultureInfo.CurrentCulture,
                $"Rain {w.PrecipRateMmH:0.0} mm/h · Gust {w.WindGustKmH:0} km/h · observed {w.ObservationTimeUtc.ToLocalTime():HH:mm} ({age:0.0} min ago)");
        }
    }

    private void Refresh()
    {
        MonitorSnapshot? snapshot = host.IsRunning ? host.LatestSnapshot : null;
        SkyImage? current = snapshot?.Image;
        if (!ReferenceEquals(current, shownImage))
        {
            shownImage = current;
            (decoded, originalWidth, originalHeight) = current is null ? (null, 0, 0) : Decode(current.JpegBytes);
            composedFor = null;
        }

        if (!ReferenceEquals(snapshot, composedFor) || decoded is null)
        {
            composedFor = snapshot;
            image = Compose(snapshot);
            RaisePropertyChanged(nameof(AllskyImage));
            RaisePropertyChanged(nameof(HasImage));
        }

        try
        {
            Metrics.Update(snapshot, PluginSettingsStore.Instance.Current);
            History.Add(snapshot);
        }
        catch (Exception e)
        {
            NINA.Core.Utility.Logger.Warning($"[AllskySafetyMonitor] Could not update the metrics: {e.Message}");
        }

        RaisePropertyChanged(nameof(IsConnected));
        RaisePropertyChanged(nameof(IsSafe));
        RaisePropertyChanged(nameof(IsAssumed));
        RaisePropertyChanged(nameof(StateText));
        RaisePropertyChanged(nameof(ReasonText));
        RaisePropertyChanged(nameof(LastUpdateText));
        RaisePropertyChanged(nameof(UntunedVisible));
        RaisePropertyChanged(nameof(ImageInfoText));
        RaisePropertyChanged(nameof(WeatherInfoText));
        RaisePropertyChanged(nameof(OverrideMode));
        RaisePropertyChanged(nameof(OverrideActive));
        RaisePropertyChanged(nameof(OverrideBanner));
        RaisePropertyChanged(nameof(CountdownText));
        RaisePropertyChanged(nameof(RoiInfoText));
        RaisePropertyChanged(nameof(MountInfoText));
        RaisePropertyChanged(nameof(OverlayLegendText));
        RaisePropertyChanged(nameof(ShowOverlay));
    }

    /// <summary>The decoded image with the overlay, or without it when switched off or if drawing fails.</summary>
    private BitmapSource? Compose(MonitorSnapshot? snapshot)
    {
        if (decoded is null || !showOverlay) return decoded;
        try
        {
            CalibrationData cal = PluginSettingsStore.Instance.Current.Calibration;
            LensCalibration lens = cal.Lens is { RadialCoefficients: [> 0, ..] } l ? l : LensProjection.Approximate(originalWidth, originalHeight);
            return OverlayImageComposer.Compose(decoded, originalWidth, lens, cal.WorkingArea, snapshot?.Roi, snapshot?.Cloud);
        }
        catch (Exception e)
        {
            NINA.Core.Utility.Logger.Warning($"[AllskySafetyMonitor] Could not draw the overlay: {e.Message}");
            return decoded;
        }
    }

    private void OpenViewer()
    {
        try
        {
            ImageViewerViewModel? vm = ImageViewerViewModel.ForLatestImage(host);
            if (vm is not null) ImageViewerWindow.Open(vm);
        }
        catch (Exception e)
        {
            NINA.Core.Utility.Logger.Warning($"[AllskySafetyMonitor] Could not open the image viewer: {e.Message}");
        }
    }

    private static string Hms(double hours)
    {
        int s = (int)Math.Round(hours * 3600) % (24 * 3600);
        return $"{s / 3600:00}h{s / 60 % 60:00}m{s % 60:00}s";
    }

    private static string Dms(double deg)
    {
        int s = (int)Math.Round(Math.Abs(deg) * 3600);
        return $"{(deg < 0 ? "−" : "+")}{s / 3600:00}°{s / 60 % 60:00}′{s % 60:00}″";
    }

    /// <summary>Decodes at panel resolution, with the original size; a JPEG that can't be decoded is simply not shown.</summary>
    private static (BitmapSource? Image, int Width, int Height) Decode(byte[] jpeg)
    {
        try
        {
            int width, height;
            using (var probe = new MemoryStream(jpeg))
            {
                BitmapFrame frame = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                (width, height) = (frame.PixelWidth, frame.PixelHeight);
            }

            using var stream = new MemoryStream(jpeg);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 800;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return (bitmap, width, height);
        }
        catch (Exception)
        {
            return (null, 0, 0);
        }
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
