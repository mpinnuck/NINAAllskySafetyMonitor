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
    public void OutOfRangeRadiusIsReported()
    {
        var s = ValidSettings();
        s.Roi.RadiusDeg = 120;
        Assert.Contains(new SettingsValidator().Validate(s), e => e.Contains("ROI radius"));
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
