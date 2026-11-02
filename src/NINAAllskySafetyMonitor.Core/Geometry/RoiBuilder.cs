using System.Globalization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Builds the ROI: the circle around the target, limited to the working area. If there is no usable
/// mount position, or less than the minimum fraction of the circle is inside the working area, the
/// whole working area is used instead and the note says why.
/// </summary>
[Requirement("REQ-23", "REQ-25", "REQ-26", "REQ-58")]
public sealed class RoiBuilder
{
    /// <summary>Points used to measure how much of the circle is inside (evenly spread over its area).</summary>
    internal const int SamplePoints = 400;

    public RegionOfInterest Build(HorizontalCoordinate? centre, string? unusableReason, double radiusDeg, double minInsideFraction, WorkingArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        if (centre is null)
        {
            return new RegionOfInterest(null, radiusDeg, true, 0, $"{unusableReason ?? "Mount position unavailable"}: judging the whole working area");
        }

        double inside = InsideFraction(centre.Value, radiusDeg, area);
        if (inside < minInsideFraction)
        {
            string pct = (inside * 100).ToString("0", CultureInfo.InvariantCulture);
            return new RegionOfInterest(centre, radiusDeg, true, inside, $"ROI {pct}% inside the working area: judging the whole working area");
        }

        return new RegionOfInterest(centre, radiusDeg, false, inside, string.Empty);
    }

    /// <summary>Fraction of the circle inside the working area, from points spread evenly over the circle (sunflower pattern).</summary>
    internal static double InsideFraction(HorizontalCoordinate centre, double radiusDeg, WorkingArea area)
    {
        int inside = 0;
        for (int k = 0; k < SamplePoints; k++)
        {
            double distance = radiusDeg * Math.Sqrt((k + 0.5) / SamplePoints);
            double bearing = k * 137.50776405;
            if (area.Contains(SkyGeometry.Destination(centre, bearing, distance))) inside++;
        }

        return (double)inside / SamplePoints;
    }
}
