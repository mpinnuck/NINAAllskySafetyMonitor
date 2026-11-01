using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Site source returning the location set by the test (null = unavailable).</summary>
public sealed class FakeSiteLocationSource : ISiteLocationSource
{
    public SiteLocation? Site { get; set; }

    public SiteLocation? GetCurrent() => Site;
}
