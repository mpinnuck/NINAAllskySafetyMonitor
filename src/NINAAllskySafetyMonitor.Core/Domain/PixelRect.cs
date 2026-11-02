namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>A rectangle of image pixels.</summary>
/// <param name="X">Left column.</param>
/// <param name="Y">Top row.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    /// <summary>This rectangle grown by <paramref name="margin"/> on every side and clipped to the image.</summary>
    public PixelRect Inflate(int margin, int imageWidth, int imageHeight)
    {
        int x0 = Math.Max(0, X - margin), y0 = Math.Max(0, Y - margin);
        int x1 = Math.Min(imageWidth, X + Width + margin), y1 = Math.Min(imageHeight, Y + Height + margin);
        return new PixelRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
    }
}
