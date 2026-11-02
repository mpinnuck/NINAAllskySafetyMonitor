using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Plugin.Imaging;
using NINAAllskySafetyMonitor.Plugin.Nina;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// The enlarged image viewer (REQ-59). View mode shows the latest Allsky image with the overlay and
/// follows new snapshots; an outside click or Esc closes it. Calibration mode shows a chosen frame and
/// hosts <see cref="CalibrationViewModel"/>; it closes only with Save or Cancel. The altitude and azimuth
/// under the mouse are read through the lens model.
/// </summary>
[Requirement("REQ-21", "REQ-45", "REQ-59")]
public sealed class ImageViewerViewModel : INotifyPropertyChanged
{
    private readonly MonitorHost? host;
    private BitmapSource source;
    private int originalWidth;
    private int originalHeight;
    private SkyImage? shownImage;
    private RegionOfInterest? roi;
    private CloudAssessment? cloud;
    private bool notSkyFrame;
    private BitmapSource? image;
    private string cursorText = string.Empty;
    private bool showWorkingArea = true;
    private bool showRoi = true;
    private bool showCompass = true;
    private bool showGrid;
    private bool showStars = true;

    private ImageViewerViewModel(string title, BitmapSource source, int originalWidth, int originalHeight, MonitorHost? host, CalibrationViewModel? calibration)
    {
        Title = title;
        this.source = source;
        this.originalWidth = originalWidth;
        this.originalHeight = originalHeight;
        this.host = host;
        Calibration = calibration;
        if (calibration is not null)
        {
            showGrid = true;
            calibration.Changed += (_, _) => Recompose();
            calibration.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        if (host is not null) host.SnapshotAvailable += OnSnapshot;
        Recompose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the window should close (Save or Cancel in calibration mode).</summary>
    public event EventHandler? CloseRequested;

    public string Title { get; }

    /// <summary>Non-null in calibration mode.</summary>
    public CalibrationViewModel? Calibration { get; }

    public bool IsCalibration => Calibration is not null;

    /// <summary>True when viewing the monitor's image (not calibrating).</summary>
    public bool IsViewMode => Calibration is null;

    /// <summary>View mode closes when the window loses focus (a click outside it).</summary>
    public bool CloseOnDeactivate => Calibration is null;

    public BitmapSource? Image
    {
        get => image;
        private set { image = value; OnPropertyChanged(); }
    }

    /// <summary>Width of the original image in pixels (mouse positions are scaled to it).</summary>
    public int OriginalWidth => originalWidth;

    public int OriginalHeight => originalHeight;

    public string CursorText
    {
        get => cursorText;
        private set { cursorText = value; OnPropertyChanged(); }
    }

    public bool ShowWorkingArea { get => showWorkingArea; set { showWorkingArea = value; OnPropertyChanged(); Recompose(); } }

    public bool ShowRoi { get => showRoi; set { showRoi = value; OnPropertyChanged(); Recompose(); } }

    public bool ShowCompass { get => showCompass; set { showCompass = value; OnPropertyChanged(); Recompose(); } }

    public bool ShowGrid { get => showGrid; set { showGrid = value; OnPropertyChanged(); Recompose(); } }

    public bool ShowStars { get => showStars; set { showStars = value; OnPropertyChanged(); Recompose(); } }

    /// <summary>View mode on the monitor's latest image; null when there is no image yet.</summary>
    public static ImageViewerViewModel? ForLatestImage(MonitorHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        MonitorSnapshot? s = host.LatestSnapshot;
        if (s?.Image is not { } img) return null;
        (int w, int h) = JpegBitmap.Size(img.JpegBytes);
        var vm = new ImageViewerViewModel("Allsky image", JpegBitmap.Load(img.JpegBytes), w, h, host, null)
        {
            shownImage = img,
        };
        vm.roi = s.Roi;
        vm.cloud = s.Cloud;
        vm.notSkyFrame = s.NotASkyFrame;
        vm.Recompose();
        return vm;
    }

    /// <summary>Calibration mode on a frame.</summary>
    public static ImageViewerViewModel ForCalibration(BitmapSource display, CalibrationViewModel calibration)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(calibration);
        return new ImageViewerViewModel("Allsky calibration", display, display.PixelWidth, display.PixelHeight, null, calibration);
    }

    /// <summary>Mouse over the image at <paramref name="p"/> (original image pixels).</summary>
    public void OnMouseMove(PixelPoint p)
    {
        HorizontalCoordinate h = new LensProjection(Lens()).ToHorizontal(p);
        CursorText = h.AltDeg < 0
            ? string.Create(CultureInfo.CurrentCulture, $"x {p.X:0}, y {p.Y:0}: below the horizon")
            : string.Create(CultureInfo.CurrentCulture, $"alt {h.AltDeg:0.0}°  az {h.AzDeg:0.0}°   (x {p.X:0}, y {p.Y:0})");
    }

    /// <summary>A click (not a drag) on the image.</summary>
    public void OnClick(PixelPoint p) => Calibration?.OnImageClick(p);

    /// <summary>Stops following snapshots.</summary>
    public void Closed()
    {
        if (host is not null) host.SnapshotAvailable -= OnSnapshot;
    }

    private LensCalibration Lens()
    {
        LensCalibration? lens = Calibration?.FittedLens ?? Calibration?.CurrentLens ?? PluginSettingsStore.Instance.Current.Calibration.Lens;
        return lens is { RadialCoefficients: [> 0, ..] } ? lens : LensProjection.Approximate(originalWidth, originalHeight);
    }

    private void Recompose()
    {
        try
        {
            CalibrationData cal = PluginSettingsStore.Instance.Current.Calibration;
            var content = new OverlayContent
            {
                WorkingArea = showWorkingArea ? cal.WorkingArea : null,
                Roi = showRoi ? roi : null,
                Compass = showCompass,
                Grid = showGrid,
                CloudStars = Calibration is null && showStars ? OverlayImageComposer.CloudStars(cloud) : Array.Empty<(PixelPoint, bool)>(),
            };
            if (Calibration is not null)
            {
                content = Calibration.Overlay(content);
                if (!showWorkingArea) content = content with { WorkingArea = null };
                if (!showStars) content = content with { Stars = Array.Empty<OverlayStar>() };
            }

            // A notice image (not a sky frame) is shown as it is: the lens model does not apply to it.
            Image = Calibration is null && notSkyFrame ? source : OverlayImageComposer.Compose(source, originalWidth, Lens(), content);
        }
        catch (Exception e)
        {
            NINA.Core.Utility.Logger.Warning($"[AllskySafetyMonitor] Could not draw the viewer overlay: {e.Message}");
            Image = source;
        }
    }

    private void OnSnapshot(object? sender, MonitorSnapshot snapshot)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (snapshot.Image is { } img && !ReferenceEquals(img, shownImage))
                {
                    shownImage = img;
                    (originalWidth, originalHeight) = JpegBitmap.Size(img.JpegBytes);
                    source = JpegBitmap.Load(img.JpegBytes);
                }

                roi = snapshot.Roi;
                cloud = snapshot.Cloud;
                notSkyFrame = snapshot.NotASkyFrame;
                Recompose();
            }
            catch (Exception e)
            {
                NINA.Core.Utility.Logger.Warning($"[AllskySafetyMonitor] Could not update the image viewer: {e.Message}");
            }
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
