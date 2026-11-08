using System.Globalization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// The simulated mounts to replay. By default a grid of sky positions [Dec −45° to +45° every 10°,
/// RA every 1 h]; when explicit targets are supplied with --target they replace the grid. At each frame
/// time the targets whose ROI is inside the working area are judged, with the ROI built exactly as the
/// plugin builds it for the mount's target.
/// </summary>
[Requirement("REQ-42")]
public sealed class TargetGrid
{
    private readonly RoiBuilder builder = new();
    private readonly bool explicitTargets;

    public TargetGrid(ReplayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Targets.Count > 0)
        {
            Targets = options.Targets;
            explicitTargets = true;
            return;
        }

        var targets = new List<ReplayTarget>();
        for (double ra = 0; ra < 24 - 1e-9; ra += options.RaStepH)
        {
            for (double dec = options.DecFromDeg; dec <= options.DecToDeg + 1e-9; dec += options.DecStepDeg)
            {
                targets.Add(new ReplayTarget(string.Create(CultureInfo.InvariantCulture, $"RA{ra:00.#} Dec{dec:+0;-0;+0}"), ra, dec));
            }
        }

        Targets = targets;
    }

    public IReadOnlyList<ReplayTarget> Targets { get; }

    /// <summary>The targets judged at this time, with their ROIs.</summary>
    public IReadOnlyList<(ReplayTarget Target, RegionOfInterest Roi)> RoisAt(DateTime utc, SiteLocation site, MonitorSettings settings, WorkingArea area)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(area);
        var result = new List<(ReplayTarget, RegionOfInterest)>();
        WorkingAreaDefinition def = area.Definition;
        foreach (ReplayTarget t in Targets)
        {
            HorizontalCoordinate centre = CoordinateConverter.ToHorizontal(t.RaHours, t.DecDeg, CoordinateEpoch.J2000, site, utc);
            if (centre.AltDeg <= 0) continue;
            if (SkyGeometry.SeparationDeg(centre, def.Centre) > def.RadiusDeg + settings.Roi.RadiusDeg) continue;
            RegionOfInterest roi = builder.Build(centre, null, settings.Roi.RadiusDeg, settings.Roi.MinInsideFraction, area);
            // An explicit --target is a simulated mount; judge it even when it falls back to the whole
            // working area, because that is what the live plugin would do. The broad grid keeps only
            // ROIs centred inside the area so it does not judge the same whole-area fallback repeatedly.
            if (explicitTargets || !roi.UsesWholeWorkingArea) result.Add((t, roi));
        }

        return result;
    }
}
