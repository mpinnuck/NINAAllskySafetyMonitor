using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>
/// Astronomical calculations needed by Core. Implemented over NINA.Astrometry (NOVAS)
/// by NINAAllskySafetyMonitor.Astrometry so that Core keeps no NINA reference.
/// </summary>
[Requirement("REQ-08", "REQ-24", "REQ-31")]
public interface IAstroCalculator
{
    /// <summary>Topocentric altitude of the Sun in degrees.</summary>
    double SunAltitudeDeg(SiteLocation site, DateTime utc);

    /// <summary>Topocentric altitude and azimuth of the Moon.</summary>
    HorizontalCoordinate MoonPosition(SiteLocation site, DateTime utc);

    /// <summary>Illuminated fraction of the Moon, 0 (new) to 1 (full).</summary>
    double MoonIllumination(DateTime utc);

    /// <summary>Converts equatorial coordinates in the given epoch to altitude and azimuth.</summary>
    HorizontalCoordinate ToHorizontal(double raHours, double decDeg, CoordinateEpoch epoch, SiteLocation site, DateTime utc);

    /// <summary>Converts altitude and azimuth to J2000 equatorial coordinates.</summary>
    (double RaHours, double DecDeg) ToEquatorial(HorizontalCoordinate horizontal, SiteLocation site, DateTime utc);
}
