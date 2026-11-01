using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Chooses the site location: the NINA profile when it supplies a usable location,
/// otherwise the configured fallback. A profile location of exactly 0°, 0° is treated as
/// "not set", because that is NINA's default for an unconfigured profile.
/// </summary>
[Requirement("REQ-04")]
public sealed class SiteLocationResolver
{
    private readonly ISiteLocationSource profileSource;
    private readonly Func<SiteSettings> fallback;

    public SiteLocationResolver(ISiteLocationSource profileSource, Func<SiteSettings> fallback)
    {
        this.profileSource = profileSource ?? throw new ArgumentNullException(nameof(profileSource));
        this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    /// <summary>Returns the site to use for this cycle.</summary>
    public SiteLocation Resolve()
    {
        SiteLocation? profile = profileSource.GetCurrent();
        if (profile is not null && IsUsable(profile)) return profile;

        SiteSettings s = fallback();
        return new SiteLocation(s.LatitudeDeg, s.LongitudeDeg, s.ElevationM);
    }

    private static bool IsUsable(SiteLocation site) =>
        double.IsFinite(site.LatitudeDeg) && double.IsFinite(site.LongitudeDeg) &&
        site.LatitudeDeg is >= -90 and <= 90 && site.LongitudeDeg is >= -180 and <= 180 &&
        !(site.LatitudeDeg == 0.0 && site.LongitudeDeg == 0.0);
}
