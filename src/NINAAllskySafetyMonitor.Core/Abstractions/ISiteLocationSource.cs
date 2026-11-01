using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>Provides the observing site from the host (the active NINA profile).</summary>
[Requirement("REQ-04")]
public interface ISiteLocationSource
{
    /// <summary>The site location, or null when the host cannot supply one.</summary>
    SiteLocation? GetCurrent();
}
