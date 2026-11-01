namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>A star found in an Allsky image.</summary>
/// <param name="Position">Centroid in pixels.</param>
/// <param name="Flux">Background-subtracted brightness.</param>
public sealed record DetectedStar(PixelPoint Position, double Flux);
