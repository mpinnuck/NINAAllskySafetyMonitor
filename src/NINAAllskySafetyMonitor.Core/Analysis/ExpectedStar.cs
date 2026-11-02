using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>A catalogue star that should be visible in the ROI, where it is on the sky and in the image.</summary>
/// <param name="Star">The catalogue star.</param>
/// <param name="Sky">Altitude and azimuth now.</param>
/// <param name="Position">Position in the image.</param>
[Requirement("REQ-29")]
public sealed record ExpectedStar(CatalogueStar Star, HorizontalCoordinate Sky, PixelPoint Position);
