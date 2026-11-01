using NINA.Profile.Interfaces;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>Reads the site location from the active NINA profile.</summary>
[Requirement("REQ-04")]
public sealed class NinaSiteLocationSource : ISiteLocationSource
{
    private readonly IProfileService profileService;

    public NinaSiteLocationSource(IProfileService profileService)
    {
        this.profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
    }

    /// <inheritdoc />
    public SiteLocation? GetCurrent()
    {
        IAstrometrySettings? a = profileService.ActiveProfile?.AstrometrySettings;
        return a is null ? null : new SiteLocation(a.Latitude, a.Longitude, a.Elevation);
    }
}
