using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Where the telescope points, from the mount's RA/Dec, epoch, the site and the time. The mount's own
/// Alt/Az are never used (REQ-06).
/// </summary>
[Requirement("REQ-06", "REQ-23", "REQ-24", "REQ-26")]
public sealed class RoiCalculator
{
    private readonly IAstroCalculator astro;
    private readonly MountStateInterpreter interpreter;

    public RoiCalculator(IAstroCalculator astro, MountStateInterpreter interpreter)
    {
        this.astro = astro ?? throw new ArgumentNullException(nameof(astro));
        this.interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
    }

    /// <summary>The ROI centre, or null with the reason when the mount position can't be used.</summary>
    public (HorizontalCoordinate? Centre, string? Unusable) CalculateCentre(MountState? m, SiteLocation site, DateTime utc)
    {
        string? why = interpreter.WhyUnusable(m);
        if (why is not null) return (null, why);
        return (astro.ToHorizontal(m!.RaHours!.Value, m.DecDeg!.Value, m.Epoch, site, utc), null);
    }
}
