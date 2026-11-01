using System.Globalization;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>Decides whether the monitor is active: the Sun must be below the configured altitude limit.</summary>
[Requirement("REQ-08", "REQ-09", "REQ-10")]
public sealed class OperatingWindow
{
    private readonly IAstroCalculator astro;

    public OperatingWindow(IAstroCalculator astro)
    {
        this.astro = astro ?? throw new ArgumentNullException(nameof(astro));
    }

    /// <summary>Evaluates the window for the site and time against <paramref name="sunAltitudeLimitDeg"/>.</summary>
    public ConditionCheck Evaluate(SiteLocation site, DateTime utc, double sunAltitudeLimitDeg)
    {
        double sun = astro.SunAltitudeDeg(site, utc);
        bool active = sun < sunAltitudeLimitDeg;
        string reason = string.Format(
            CultureInfo.InvariantCulture,
            active ? "Sun at {0:0.0}° (below limit {1:0.#}°)" : "Outside operating window: Sun at {0:0.0}° (limit {1:0.#}°)",
            sun,
            sunAltitudeLimitDeg);
        return new ConditionCheck(SafetyCondition.ActiveWindow, active, reason);
    }
}
