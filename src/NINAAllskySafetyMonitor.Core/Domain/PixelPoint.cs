namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Position in an Allsky image, in pixels from the top-left corner.</summary>
/// <param name="X">Column.</param>
/// <param name="Y">Row.</param>
public readonly record struct PixelPoint(double X, double Y);
