namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Set of image pixels that belong to a region (for example the ROI).</summary>
public sealed class PixelMask
{
    private readonly bool[] bits;

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
    }
}
