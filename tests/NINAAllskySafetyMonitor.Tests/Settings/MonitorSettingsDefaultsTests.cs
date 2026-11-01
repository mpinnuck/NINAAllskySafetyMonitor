using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Settings;

/// <summary>Defaults match the bracketed values in requirements v4.</summary>
public sealed class MonitorSettingsDefaultsTests
{
    private readonly MonitorSettings s = new();

    [Fact, Requirement("REQ-04")]
    public void SiteDefaultsToStation() =>
        Assert.Equal((-33.713562, 151.09024, 134.0), (s.Site.LatitudeDeg, s.Site.LongitudeDeg, s.Site.ElevationM));

    [Fact, Requirement("REQ-09")]
    public void OperatingWindowIsAstronomicalTwilight() => Assert.Equal(-18.0, s.Window.SunAltitudeLimitDeg);

    [Fact, Requirement("REQ-11", "REQ-13")]
    public void ImageSourceDefaults()
    {
        Assert.Equal(ImageSourceKind.Direct, s.ImageSource.Kind);
        Assert.Equal("https://oci.microconcepts.com.au/allsky/", s.ImageSource.Web.BaseUrl);
        Assert.Equal("image.jpg", s.ImageSource.Web.FileName);
        Assert.Equal("allsky.local", s.ImageSource.Direct.Host);
        Assert.Equal("192.168.1.110", s.ImageSource.Direct.FallbackIp);
        Assert.Equal("/home/pi/allsky/tmp/allsky.jpg", s.ImageSource.Direct.RemotePath);
    }

    [Fact, Requirement("REQ-14", "REQ-15", "REQ-17", "REQ-18")]
    public void AcquisitionTimingDefaults()
    {
        Assert.Equal(60, s.ImageSource.IntervalS);
        Assert.Equal(10, s.ImageSource.StaleAfterMin);
        Assert.Equal(10, s.ImageSource.FetchFailUnsafeMin);
        Assert.Equal(5, s.ImageSource.IncompleteRetryS);
        Assert.Equal(3, s.ImageSource.IncompleteRetries);
    }

    [Fact, Requirement("REQ-52")]
    public void HostKeyFingerprintIsPinned() =>
        Assert.Equal("SHA256:xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg", s.ImageSource.Direct.HostKeyFingerprint);

    [Fact, Requirement("REQ-12")]
    public void KeyAuthenticationIsDefault()
    {
        Assert.False(s.ImageSource.Direct.UsePassword);
        Assert.EndsWith("id_ed25519", s.ImageSource.Direct.KeyFile);
    }

    [Fact, Requirement("REQ-23")]
    public void RoiRadiusIs15Degrees() => Assert.Equal(15.0, s.Roi.RadiusDeg);

    [Fact, Requirement("REQ-29", "REQ-30", "REQ-31", "REQ-43")]
    public void CloudThresholdDefaults()
    {
        Assert.Equal(4.0, s.Cloud.LimitingMagnitude);
        Assert.Equal(0.70, s.Cloud.MinStarFraction);
        Assert.Equal(8, s.Cloud.MinExpectedStars);
        Assert.Equal(1.3, s.Cloud.MaxBrightnessRatio);
        Assert.Equal(1.5, s.Cloud.MaxUniformityRatio);
        Assert.Equal(15.0, s.Cloud.MoonExclusionDeg);
        Assert.False(s.Cloud.Tuned);
    }

    [Fact, Requirement("REQ-32", "REQ-35", "REQ-36")]
    public void WeatherDefaults()
    {
        Assert.True(s.Weather.Enabled);
        Assert.Equal("ISYDNEY478", s.Weather.StationId);
        Assert.Equal(120, s.Weather.PollIntervalS);
        Assert.Equal(40.0, s.Weather.MaxGustKmH);
        Assert.Equal(15, s.Weather.StaleAfterMin);
    }

    [Fact, Requirement("REQ-39")]
    public void SafeAfterFourMinutes() => Assert.Equal(4, s.Decision.SafeAfterMin);

    [Fact, Requirement("REQ-41")]
    public void TuningOffByDefault() => Assert.False(s.Tuning.Enabled);

    [Fact, Requirement("REQ-44")]
    public void LogsKeptSevenDays() => Assert.Equal(7, s.Logging.RetentionDays);
}
