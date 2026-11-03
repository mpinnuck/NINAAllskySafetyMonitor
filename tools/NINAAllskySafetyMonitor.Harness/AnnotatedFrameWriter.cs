using System.Globalization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using SkiaSharp;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// Saves a marked-up copy of a frame for checking by eye: the working area (yellow dashed) and its outline
/// (blue), the target's ROI (green), the expected stars (green found, red missing), and a banner with the
/// time, target, verdict, metrics and why the frame was picked.
/// </summary>
[Requirement("REQ-61")]
public sealed class AnnotatedFrameWriter
{
    private static readonly SKColor Yellow = new(0xFF, 0xD5, 0x4F);
    private static readonly SKColor Green = new(0x4C, 0xE0, 0x6A);
    private static readonly SKColor Red = new(0xFF, 0x4B, 0x4B);
    private static readonly SKColor Blue = new(0x4F, 0xC3, 0xF7);

    private readonly LensCalibration lens;
    private readonly WorkingAreaDefinition area;
    private readonly string outputFolder;
    private readonly TimeZoneInfo zone;

    public AnnotatedFrameWriter(LensCalibration lens, WorkingAreaDefinition area, string outputFolder, TimeZoneInfo zone)
    {
        this.lens = lens ?? throw new ArgumentNullException(nameof(lens));
        this.area = area ?? throw new ArgumentNullException(nameof(area));
        this.outputFolder = outputFolder ?? throw new ArgumentNullException(nameof(outputFolder));
        this.zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    /// <summary>Writes the copy and returns its path relative to the output folder.</summary>
    public string Write(SavedFrame frame, TargetResult target, string why)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(target);
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(frame.CaptureUtc, zone);
        string night = frame.Night.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string name = $"{local:HHmmss}-{Safe(target.Target.Id)}.jpg";
        string relative = Path.Combine("review", night, name);
        string full = Path.Combine(outputFolder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        using SKBitmap? bitmap = SKBitmap.Decode(frame.Path);
        if (bitmap is null) throw new InvalidDataException($"Not a readable image: {frame.Path}");
        using (var canvas = new SKCanvas(bitmap))
        {
            Draw(canvas, bitmap.Width, target, $"{local:yyyy-MM-dd HH:mm:ss} · {target.Target.Id} · {target.Verdict.ToString().ToUpperInvariant()} · {target.Cloud.Summary}", why);
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using (FileStream f = File.Create(full)) data.SaveTo(f);
        return relative.Replace('\\', '/');
    }

    private void Draw(SKCanvas canvas, int width, TargetResult target, string title, string why)
    {
        var projection = new LensProjection(lens);
        float k = Math.Max(1f, width / 800f);
        SKPoint Sky(HorizontalCoordinate h)
        {
            PixelPoint p = projection.ToPixel(h.AltDeg < 0 ? h with { AltDeg = 0 } : h);
            return new SKPoint((float)p.X, (float)p.Y);
        }

        using var areaPen = Pen(Yellow, 2 * k, dashed: true);
        canvas.DrawPath(Circle(area.Centre, area.RadiusDeg, Sky), areaPen);
        if (area.Outline.Count >= 3)
        {
            using var outlinePen = Pen(Blue, 2 * k, dashed: false);
            using var outline = new SKPath();
            outline.AddPoly(area.Outline.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToArray(), close: true);
            canvas.DrawPath(outline, outlinePen);
        }

        if (target.Roi.Centre is HorizontalCoordinate c)
        {
            using var roiPen = Pen(Green, 3 * k, dashed: false);
            canvas.DrawPath(Circle(c, target.Roi.RadiusDeg, Sky), roiPen);
        }

        using var found = Pen(Green, 1.5f * k, dashed: false);
        using var missing = Pen(Red, 1.5f * k, dashed: false);
        for (int i = 0; i < target.Cloud.Expected.Count; i++)
        {
            PixelPoint p = target.Cloud.Expected[i];
            bool ok = i < target.Cloud.Found.Count && target.Cloud.Found[i];
            canvas.DrawCircle((float)p.X, (float)p.Y, 6 * k, ok ? found : missing);
        }

        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 170), Style = SKPaintStyle.Fill };
        using var text = new SKPaint { Color = SKColors.White, TextSize = 14 * k, IsAntialias = true, Typeface = SKTypeface.Default };
        canvas.DrawRect(0, 0, width, 46 * k, back);
        canvas.DrawText(title, 8 * k, 18 * k, text);
        canvas.DrawText("Picked: " + why, 8 * k, 38 * k, text);
    }

    private static SKPath Circle(HorizontalCoordinate centre, double radiusDeg, Func<HorizontalCoordinate, SKPoint> toPoint)
    {
        var path = new SKPath();
        for (int b = 0; b <= 360; b += 2)
        {
            SKPoint p = toPoint(SkyGeometry.Destination(centre, b, radiusDeg));
            if (b == 0) path.MoveTo(p);
            else path.LineTo(p);
        }

        path.Close();
        return path;
    }

    private static SKPaint Pen(SKColor colour, float width, bool dashed) => new()
    {
        Color = colour,
        StrokeWidth = width,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        PathEffect = dashed ? SKPathEffect.CreateDash(new[] { 4 * width, 3 * width }, 0) : null,
    };

    private static string Safe(string id) => id.Replace(' ', '_').Replace('+', 'p').Replace('-', 'm');
}
