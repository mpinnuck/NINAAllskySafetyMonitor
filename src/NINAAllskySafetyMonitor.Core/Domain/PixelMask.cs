namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Set of image pixels that belong to a region (for example the ROI).</summary>
public sealed class PixelMask
{
    private readonly bool[] bits;
    private int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

    /// <summary>Creates an empty mask for an image of the given size.</summary>
    public PixelMask(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        bits = new bool[width * height];
    }

    /// <summary>Image width in pixels.</summary>
    public int Width { get; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; }

    /// <summary>Number of pixels in the mask.</summary>
    public int Count { get; private set; }

    /// <summary>Smallest rectangle holding every pixel in the mask, or null when the mask is empty.</summary>
    public PixelRect? Bounds => Count == 0 ? null : new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);

    /// <summary>True when the pixel is inside the image and in the mask.</summary>
    public bool Contains(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height && bits[(y * Width) + x];

    /// <summary>Adds the pixel to the mask. Pixels outside the image are ignored.</summary>
    public void Add(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        int i = (y * Width) + x;
        if (bits[i]) return;
        bits[i] = true;
        Count++;
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
    }
}
