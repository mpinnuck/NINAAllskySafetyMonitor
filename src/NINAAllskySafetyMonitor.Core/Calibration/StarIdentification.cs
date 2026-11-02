using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>A star the imager has named and clicked on the frame.</summary>
/// <param name="Star">The catalogue star chosen.</param>
/// <param name="Position">Where it was clicked, in image pixels.</param>
public sealed record StarIdentification(CatalogueStar Star, PixelPoint Position);
