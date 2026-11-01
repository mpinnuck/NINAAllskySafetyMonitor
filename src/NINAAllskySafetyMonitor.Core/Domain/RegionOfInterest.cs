using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>The circular region of sky analysed for cloud, and its pixels in the image.</summary>
/// <param name="Centre">Centre of the circle on the sky.</param>
/// <param name="RadiusDeg">Radius on the sky in degrees.</param>
/// <param name="IsZenithFallback">True when centred on the zenith because the mount position was unusable.</param>
/// <param name="Mask">Image pixels inside the circle and above the horizon mask.</param>
[Requirement("REQ-23", "REQ-26")]
public sealed record RegionOfInterest(HorizontalCoordinate Centre, double RadiusDeg, bool IsZenithFallback, PixelMask Mask);
