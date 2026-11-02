using System.IO;
using System.Windows.Media.Imaging;

namespace NINAAllskySafetyMonitor.Plugin.Imaging;

/// <summary>Loads a JPEG for display, frozen so it can cross threads.</summary>
public static class JpegBitmap
{
    /// <summary>The image, scaled to <paramref name="decodeWidth"/> pixels wide when given (full size otherwise).</summary>
    public static BitmapSource Load(byte[] jpeg, int? decodeWidth = null)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        using var stream = new MemoryStream(jpeg);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (decodeWidth is int w) bitmap.DecodePixelWidth = w;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Width and height of the JPEG without decoding its pixels.</summary>
    public static (int Width, int Height) Size(byte[] jpeg)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        using var stream = new MemoryStream(jpeg);
        BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }
}
