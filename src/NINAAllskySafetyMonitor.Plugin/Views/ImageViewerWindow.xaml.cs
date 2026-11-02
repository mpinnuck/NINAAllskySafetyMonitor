using System.Windows;
using System.Windows.Input;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Plugin.ViewModels;

namespace NINAAllskySafetyMonitor.Plugin.Views;

/// <summary>
/// Large image viewer over NINA's window (REQ-59). Zoom (wheel), pan (drag) and mouse-to-pixel mapping
/// are view concerns handled here; everything else is in <see cref="ImageViewerViewModel"/>. In view mode
/// a click outside the window (it loses focus) or Esc closes it; calibration mode closes only through its
/// Save and Cancel buttons.
/// </summary>
public partial class ImageViewerWindow : Window
{
    private const double DragThreshold = 4;
    private readonly ImageViewerViewModel vm;
    private Point downAt;
    private Point panAtDown;
    private bool mouseDown;
    private bool dragged;
    private bool activated;

    public ImageViewerWindow(ImageViewerViewModel vm)
    {
        this.vm = vm ?? throw new ArgumentNullException(nameof(vm));
        InitializeComponent();
        DataContext = vm;

        Window? owner = Application.Current?.MainWindow;
        if (owner is not null && !ReferenceEquals(owner, this))
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

        vm.CloseRequested += (_, _) => Close();
        Activated += (_, _) => activated = true;
        Deactivated += (_, _) =>
        {
            if (vm.CloseOnDeactivate && activated) Close();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && vm.CloseOnDeactivate) Close();
        };
        Closed += (_, _) => vm.Closed();
    }

    /// <summary>Opens the viewer for <paramref name="vm"/>.</summary>
    public static void Open(ImageViewerViewModel vm)
    {
        var window = new ImageViewerWindow(vm);
        window.Show();
        window.Activate();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
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
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!mouseDown) return;
        mouseDown = false;
        Viewport.ReleaseMouseCapture();
        if (!dragged && ImagePixel(e) is PixelPoint p) vm.OnClick(p);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
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
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
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
    }

    /// <summary>The original-image pixel under the mouse, or null when outside the image.</summary>
    private PixelPoint? ImagePixel(MouseEventArgs e)
    {
        if (Picture.ActualWidth <= 0 || Picture.ActualHeight <= 0) return null;
        Point u = e.GetPosition(Picture);
        if (u.X < 0 || u.Y < 0 || u.X > Picture.ActualWidth || u.Y > Picture.ActualHeight) return null;
        return new PixelPoint(u.X * vm.OriginalWidth / Picture.ActualWidth, u.Y * vm.OriginalHeight / Picture.ActualHeight);
    }
}
