using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Settings;

[Requirement("REQ-46")]
public sealed class SettingsValidatorTests
{
    private static MonitorSettings ValidSettings()
    {
        var s = new MonitorSettings();
        s.Weather.ApiKey = "test-key";
        return s;
    }

    [Fact]
    public void DefaultsWithApiKeyAreValid() =>
        Assert.Empty(new SettingsValidator().Validate(ValidSettings()));

    [Fact]
    public void MissingApiKeyIsReportedWhenWeatherEnabled()
    {
        var s = new MonitorSettings();
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("API key"));
    }

    [Fact]
    public void MissingApiKeyIsAcceptedWhenWeatherDisabled()
    {
        var s = new MonitorSettings();
        s.Weather.Enabled = false;
        Assert.Empty(new SettingsValidator().Validate(s));
    }

    [Fact]
    [Requirement("REQ-41")]
    public void TuningRetentionIsChecked()
    {
        MonitorSettings s = ValidSettings();
        s.Tuning.RetentionDays = 0;
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("Tuning data retention"));
    }

    [Fact]
    public void OutOfRangeRadiusIsReported()
    {
        var s = ValidSettings();
        s.Roi.RadiusDeg = 120;
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("ROI radius"));
    }

    [Theory, Requirement("REQ-22")]
    [InlineData(-1, 180, 60, "centre altitude")]
    [InlineData(75, 361, 60, "centre azimuth")]
    [InlineData(75, 180, 4, "radius")]
    [InlineData(75, 180, 91, "radius")]
    public void OutOfRangeWorkingAreaIsReported(double alt, double az, double radius, string expected)
    {
        var s = ValidSettings();
        s.Calibration.WorkingArea = s.Calibration.WorkingArea with { CentreAltDeg = alt, CentreAzDeg = az, RadiusDeg = radius };
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("Working area " + expected));
    }

    [Theory, Requirement("REQ-58")]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void OutOfRangeInsideFractionIsReported(double fraction)
    {
        var s = ValidSettings();
        s.Roi.MinInsideFraction = fraction;
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("inside the working area"));
    }

    [Theory, Requirement("REQ-27")]
    [InlineData(0)]
    [InlineData(61)]
    public void OutOfRangeMountIntervalIsReported(int seconds)
    {
        var s = ValidSettings();
        s.Mount.PollIntervalS = seconds;
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("Mount check interval"));
    }

    [Theory, Requirement("REQ-20")]
    [InlineData(2, 0.2, 4.5, "Minimum matched stars")]
    [InlineData(15, 0, 4.5, "Maximum lens fit error")]
    [InlineData(15, 0.2, 5.0, "Catalogue limit")]
    public void OutOfRangeLensFitLimitsAreReported(int stars, double errorDeg, double mag, string expected)
    {
        var s = ValidSettings();
        s.LensFit.MinMatchedStars = stars;
        s.LensFit.MaxFitErrorDeg = errorDeg;
        s.LensFit.CatalogueLimitMagnitude = mag;
        Assert.Contains(new SettingsValidator().Validate(s), e => e.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact, Requirement("REQ-19")]
    public void LensModelWithoutARadiusIsReported()
    {
        var s = ValidSettings();
        s.Calibration.Lens = new Core.Geometry.LensCalibration { CentreX = 100, CentreY = 100 };
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("horizon radius"));
    }

    [Theory]
    [InlineData(-30.0)]
    [InlineData(-18.0)]
    [InlineData(90.0)]
    public void SunLimitAcceptsTwilightToDaytimeTesting(double limit)
    {
        var s = ValidSettings();
        s.Window.SunAltitudeLimitDeg = limit;
        Assert.Empty(new SettingsValidator().Validate(s));
    }

    [Theory]
    [InlineData(-31.0)]
    [InlineData(91.0)]
    public void SunLimitOutsideRangeIsReported(double limit)
    {
        var s = ValidSettings();
        s.Window.SunAltitudeLimitDeg = limit;
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("Sun altitude limit"));
    }

    [Fact]
    public void MissingHostKeyTypeIsReported()
    {
        var s = ValidSettings();
        s.ImageSource.Direct.HostKeyAlgorithm = " ";
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("host-key type"));
    }

    [Fact]
    public void WebSourceNeedsAbsoluteUrl()
    {
        var s = ValidSettings();
        s.ImageSource.Kind = ImageSourceKind.Web;
        s.ImageSource.Web.BaseUrl = "not a url";
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("absolute URL"));
    }

    [Fact]
    public void DirectSourceNeedsKeyFileUnlessPassword()
    {
        var s = ValidSettings();
        s.ImageSource.Direct.KeyFile = "";
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("SSH key"));

        s.ImageSource.Direct.UsePassword = true;
        Assert.DoesNotContain(new SettingsValidator().Validate(s), e => e.Contains("SSH key"));
    }
}
