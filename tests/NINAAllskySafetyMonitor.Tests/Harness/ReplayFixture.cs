using System.Globalization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Harness;
using NINAAllskySafetyMonitor.Tests.Fakes;
using SkiaSharp;

namespace NINAAllskySafetyMonitor.Tests.Harness;

/// <summary>A temporary Allsky images folder with synthetic frames, and a settings file for them.</summary>
public sealed class ReplayFixture : IDisposable
{
    public static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    public ReplayFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "NASM-Replay-" + Guid.NewGuid().ToString("N"));
        Images = Path.Combine(Root, "images");
        Directory.CreateDirectory(Images);
        var s = new MonitorSettings();
        s.Calibration.Lens = SyntheticCloudSky.Lens;
        SettingsFile = Path.Combine(Root, "settings.json");
        new SettingsStore(SettingsFile).Save(s);
    }

    public string Root { get; }

    public string Images { get; }

    public string SettingsFile { get; }

    public string Out => Path.Combine(Root, "out");

    public ReplayOptions Options(int step = 60) => new()
    {
        FramesFolder = Images,
        SettingsFile = SettingsFile,
        OutputFolder = Out,
        StepS = step,
        Zone = Sydney,
    };

    /// <summary>Saves a frame as Allsky names it: images/yyyyMMdd(night)/allsky-yyyyMMddHHmmss(local).jpg.</summary>
    public string Save(GrayFrame frame, DateTime utc, string? subFolder = null)
    {
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc, Sydney);
        string night = local.AddHours(-12).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Images, night, subFolder ?? string.Empty);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"allsky-{local:yyyyMMddHHmmss}.jpg");
        File.WriteAllBytes(path, Jpeg(frame));
        return path;
    }

    public static byte[] Jpeg(GrayFrame frame)
    {
        using var bitmap = new SKBitmap(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        for (int y = 0; y < frame.Height; y++)
        {
            for (int x = 0; x < frame.Width; x++)
            {
                byte v = (byte)Math.Clamp(frame.At(x, y), 0, 255);
                bitmap.SetPixel(x, y, new SKColor(v, v, v));
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
