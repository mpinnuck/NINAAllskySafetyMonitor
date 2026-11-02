using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Tests whether a direction is in the working area: above the horizon, inside the circle, and (when an
/// outline is drawn and a lens model is known) inside the outline.
/// </summary>
[Requirement("REQ-22", "REQ-25")]
public sealed class WorkingArea
{
    private readonly WorkingAreaDefinition definition;
    private readonly LensProjection? projection;

    public WorkingArea(WorkingAreaDefinition definition, LensProjection? projection)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.projection = projection;
    }

    public WorkingAreaDefinition Definition => definition;

    public bool Contains(HorizontalCoordinate h)
    {
        if (h.AltDeg <= 0) return false;
        if (SkyGeometry.SeparationDeg(h, definition.Centre) > definition.RadiusDeg) return false;
        if (definition.Outline.Count < 3 || projection is null) return true;
        return InsidePolygon(projection.ToPixel(h), definition.Outline);
    }

    /// <summary>Even-odd rule point-in-polygon test.</summary>
    internal static bool InsidePolygon(PixelPoint p, IReadOnlyList<PixelPoint> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            PixelPoint a = polygon[i], b = polygon[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < ((b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) + a.X) inside = !inside;
        }

        return inside;
    }
}
