using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Plugin.Nina;
using NINAAllskySafetyMonitor.Plugin.ViewModels;

namespace NINAAllskySafetyMonitor.Plugin.Views;

/// <summary>
/// Large image viewer over NINA's window (REQ-59). Zoom (wheel), pan (drag) and mouse-to-pixel mapping
/// are view concerns handled here; everything else is in <see cref="ImageViewerViewModel"/>. In view mode
/// a click outside the window (it loses focus) or Esc closes it; calibration mode closes only through its
/// Save and Cancel buttons. No exception from this window may reach NINA.
/// </summary>
public partial class ImageViewerWindow : Window
{
    private static readonly NinaLogSink Log = new();
    private const double DragThreshold = 4;
    private static readonly TimeSpan OpeningGrace = TimeSpan.FromMilliseconds(600);
    private readonly ImageViewerViewModel vm;
    private readonly DateTime openedUtc = DateTime.UtcNow;
    private Point downAt;
    private Point panAtDown;
    private bool mouseDown;
    private bool dragged;
    private bool activated;
    private bool closing;

    public ImageViewerWindow(ImageViewerViewModel vm)
    {
        this.vm = vm ?? throw new ArgumentNullException(nameof(vm));
        InitializeComponent();
        DataContext = vm;

        Window? owner = Application.Current?.MainWindow;
        if (owner is not null && !ReferenceEquals(owner, this) && owner.IsLoaded)
        {
            Owner = owner;
            Width = Math.Max(600, owner.ActualWidth * 0.9);
            Height = Math.Max(450, owner.ActualHeight * 0.9);
        }
        else
        {
            Width = 1200;
            Height = 900;
        }

        vm.CloseRequested += (_, _) => RequestClose();
        Activated += (_, _) => activated = true;
        Deactivated += (_, _) =>
        {
            // A window is also deactivated while it closes, and just after opening when the click that
            // opened it finishes in NINA's window; neither may close it (again).
            if (vm.CloseOnDeactivate && activated && !closing && DateTime.UtcNow - openedUtc > OpeningGrace) RequestClose();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && vm.CloseOnDeactivate) RequestClose();
        };
        Closing += (_, _) => closing = true;
        Closed += (_, _) => Guard(vm.Closed);
    }

    /// <summary>Opens the viewer for <paramref name="vm"/> once the current input event has finished.</summary>
    public static void Open(ImageViewerViewModel vm)
    {
        Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => Guard(() =>
        {
            var window = new ImageViewerWindow(vm);
            window.Show();
            window.Activate();
        })));
    }

    /// <summary>Closes the window later on the dispatcher, once, never while it is already closing.</summary>
    private void RequestClose()
    {
        if (closing) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!closing) Guard(Close);
        }));
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e) => Guard(() =>
    {
        if (e.ClickCount == 2)
        {
            Zoom.ScaleX = Zoom.ScaleY = 1;
            Pan.X = Pan.Y = 0;
            return;
        }

        mouseDown = true;
        dragged = false;
        downAt = e.GetPosition(Viewport);
        panAtDown = new Point(Pan.X, Pan.Y);
        Viewport.CaptureMouse();
    });

    private void OnMouseUp(object sender, MouseButtonEventArgs e) => Guard(() =>
    {
        if (!mouseDown) return;
        mouseDown = false;
        Viewport.ReleaseMouseCapture();
        if (!dragged && ImagePixel(e) is PixelPoint p) vm.OnClick(p);
    });

    private void OnMouseMove(object sender, MouseEventArgs e) => Guard(() =>
    {
        if (mouseDown)
        {
            Point now = e.GetPosition(Viewport);
            if (dragged || (now - downAt).Length > DragThreshold)
            {
                dragged = true;
                Pan.X = panAtDown.X + (now.X - downAt.X);
                Pan.Y = panAtDown.Y + (now.Y - downAt.Y);
            }
        }

        if (ImagePixel(e) is PixelPoint p) vm.OnMouseMove(p);
    });

    private void OnMouseWheel(object sender, MouseWheelEventArgs e) => Guard(() =>
    {
        double old = Zoom.ScaleX;
        double next = Math.Clamp(old * (e.Delta > 0 ? 1.25 : 0.8), 1, 30);
        Point u = e.GetPosition(Picture); // untransformed image-element coordinates
        Pan.X += u.X * (old - next);
        Pan.Y += u.Y * (old - next);
        Zoom.ScaleX = Zoom.ScaleY = next;
        if (next == 1)
        {
            Pan.X = Pan.Y = 0;
        }
    });

    /// <summary>The original-image pixel under the mouse, or null when outside the image.</summary>
    private PixelPoint? ImagePixel(MouseEventArgs e)
    {
        if (Picture.ActualWidth <= 0 || Picture.ActualHeight <= 0 || vm.OriginalWidth <= 0) return null;
        Point u = e.GetPosition(Picture);
        double scale = Math.Min(Picture.ActualWidth / vm.OriginalWidth, Picture.ActualHeight / vm.OriginalHeight);
        double width = vm.OriginalWidth * scale;
        double height = vm.OriginalHeight * scale;
        double left = (Picture.ActualWidth - width) / 2;
        double top = (Picture.ActualHeight - height) / 2;
        if (u.X < left || u.Y < top || u.X > left + width || u.Y > top + height) return null;
        return new PixelPoint((u.X - left) / scale, (u.Y - top) / scale);
    }

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Write(LogLevel.Error, $"Image viewer: {e}");
        }
    }
}
