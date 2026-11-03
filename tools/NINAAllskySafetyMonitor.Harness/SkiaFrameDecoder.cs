using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using SkiaSharp;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// Decodes a JPEG into the darkest of red, green and blue per pixel, as the plugin's WpfFrameDecoder does,
/// using SkiaSharp so it runs on macOS and Windows.
/// </summary>
[Requirement("REQ-42")]
public sealed class SkiaFrameDecoder : IFrameDecoder
{
    public GrayFrame Decode(byte[] jpeg)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        using SKBitmap? decoded = SKBitmap.Decode(jpeg);
        if (decoded is null) throw new InvalidDataException("Not a readable image.");
        using SKBitmap bgra = decoded.ColorType == SKColorType.Bgra8888 ? decoded.Copy() : decoded.Copy(SKColorType.Bgra8888);
        int w = bgra.Width, h = bgra.Height;
        ReadOnlySpan<byte> bytes = bgra.GetPixelSpan();
        var pixels = new float[w * h];
        int stride = bgra.RowBytes;
        for (int y = 0; y < h; y++)
        {
            int row = y * stride;
            for (int x = 0; x < w; x++)
            {
                int j = row + (x * 4);
                pixels[(y * w) + x] = Math.Min(bytes[j], Math.Min(bytes[j + 1], bytes[j + 2]));
            }
        }

        return new GrayFrame(w, h, pixels);
    }
}
