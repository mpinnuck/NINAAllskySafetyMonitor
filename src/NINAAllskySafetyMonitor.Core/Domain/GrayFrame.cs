namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>A decoded image as luminance values, row-major, one float per pixel.</summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Pixels">Luminance values, length Width × Height.</param>
public sealed record GrayFrame(int Width, int Height, float[] Pixels)
{
    /// <summary>Luminance at column <paramref name="x"/>, row <paramref name="y"/>.</summary>
    public float At(int x, int y) => Pixels[(y * Width) + x];
}
