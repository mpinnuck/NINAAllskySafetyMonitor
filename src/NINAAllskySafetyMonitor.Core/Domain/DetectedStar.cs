namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>A star found in an Allsky image.</summary>
/// <param name="Position">Centroid in pixels.</param>
/// <param name="Flux">Background-subtracted brightness summed over the spot.</param>
/// <param name="Peak">Brightest background-subtracted pixel; stars are sharp, so this ranks them better than flux.</param>
/// <param name="AreaPx">Number of pixels in the spot.</param>
public sealed record DetectedStar(PixelPoint Position, double Flux, double Peak = 0, int AreaPx = 0);
