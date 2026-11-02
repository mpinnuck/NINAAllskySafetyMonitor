using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>A catalogue star matched to a detected star by the lens fit.</summary>
/// <param name="Star">The catalogue star and its sky position.</param>
/// <param name="Detected">Where it was found in the image.</param>
/// <param name="Predicted">Where the fitted lens model puts it.</param>
/// <param name="ErrorPx">Distance between the two, in pixels.</param>
/// <param name="ErrorDeg">The same on the sky, in degrees.</param>
public sealed record StarMatch(SkyStar Star, PixelPoint Detected, PixelPoint Predicted, double ErrorPx, double ErrorDeg);
