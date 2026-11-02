using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;

namespace NINAAllskySafetyMonitor.Plugin.Imaging;

/// <summary>What to draw over an Allsky image. Pixel positions are in the original image's pixels.</summary>
public sealed record OverlayContent
{
    /// <summary>The working area (circle and drawn outline), or null to leave it out.</summary>
    public WorkingAreaDefinition? WorkingArea { get; init; }

    /// <summary>The ROI, or null to leave it out.</summary>
    public RegionOfInterest? Roi { get; init; }

    /// <summary>N, E, S, W labels just above the horizon.</summary>
    public bool Compass { get; init; } = true;

    /// <summary>Altitude circles (0°, 30°, 60°) and azimuth lines every 45°.</summary>
    public bool Grid { get; init; }

    /// <summary>Catalogue stars to mark.</summary>
    public IReadOnlyList<OverlayStar> Stars { get; init; } = Array.Empty<OverlayStar>();

    /// <summary>Stars the cloud test expected in the ROI: found (green) or missing (red).</summary>
    public IReadOnlyList<(PixelPoint Position, bool Found)> CloudStars { get; init; } = Array.Empty<(PixelPoint, bool)>();

    /// <summary>An outline being drawn (not yet saved).</summary>
    public IReadOnlyList<PixelPoint> DraftOutline { get; init; } = Array.Empty<PixelPoint>();

    /// <summary>Stars clicked when identifying them.</summary>
    public IReadOnlyList<(PixelPoint Position, string Label)> Picks { get; init; } = Array.Empty<(PixelPoint, string)>();
}

/// <summary>A catalogue star drawn on the image: green when matched by the fit, grey otherwise.</summary>
public sealed record OverlayStar(PixelPoint Position, string? Label, bool Matched);
