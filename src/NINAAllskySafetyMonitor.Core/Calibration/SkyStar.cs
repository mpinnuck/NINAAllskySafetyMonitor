using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>A catalogue star and where it was on the sky at the frame's time.</summary>
public sealed record SkyStar(CatalogueStar Star, HorizontalCoordinate Position);
