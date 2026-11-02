using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.Imaging;

/// <summary>Decodes a JPEG with WPF into the darkest colour channel per pixel (see <see cref="IFrameDecoder"/>).</summary>
[Requirement("REQ-20")]
public sealed class WpfFrameDecoder : IFrameDecoder
{
    /// <inheritdoc />
    public GrayFrame Decode(byte[] jpeg)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        using var stream = new MemoryStream(jpeg);
        BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var bgr = new FormatConvertedBitmap(frame, PixelFormats.Bgr32, null, 0);
        int w = bgr.PixelWidth, h = bgr.PixelHeight, stride = w * 4;
        var bytes = new byte[stride * h];
        bgr.CopyPixels(bytes, stride, 0);
        var pixels = new float[w * h];
        for (int i = 0, j = 0; i < pixels.Length; i++, j += 4)
        {
            pixels[i] = Math.Min(bytes[j], Math.Min(bytes[j + 1], bytes[j + 2]));
        }

        return new GrayFrame(w, h, pixels);
    }
}
