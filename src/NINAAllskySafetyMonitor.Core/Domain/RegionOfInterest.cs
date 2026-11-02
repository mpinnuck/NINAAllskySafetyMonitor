using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>The region of sky judged for cloud this cycle.</summary>
/// <param name="Centre">Where the telescope points (alt/az), or null when the mount position is unavailable.</param>
/// <param name="RadiusDeg">Radius of the ROI circle on the sky.</param>
/// <param name="UsesWholeWorkingArea">True when the whole working area is judged instead of the circle (REQ-26, REQ-58).</param>
/// <param name="InsideFraction">Fraction of the circle inside the working area (0 when there is no centre).</param>
/// <param name="Note">Why the whole working area is used; empty otherwise.</param>
/// <param name="Mask">Image pixels to analyse; built with cloud detection (slice 6).</param>
[Requirement("REQ-23", "REQ-25", "REQ-26", "REQ-58")]
public sealed record RegionOfInterest(
    HorizontalCoordinate? Centre,
    double RadiusDeg,
    bool UsesWholeWorkingArea,
    double InsideFraction,
    string Note,
    PixelMask? Mask = null);
