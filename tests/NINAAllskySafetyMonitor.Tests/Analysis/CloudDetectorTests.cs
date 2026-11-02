using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;
using Xunit.Abstractions;

namespace NINAAllskySafetyMonitor.Tests.Analysis;

[Requirement("REQ-28", "REQ-29", "REQ-30", "REQ-31")]
public sealed class CloudDetectorTests
{
    private static readonly GrayFrame Clear = SyntheticCloudSky.Frame();
    private static readonly MoonState MoonDown = new(new HorizontalCoordinate(-30, 0), 0.5);
    private readonly CloudDetectionSettings settings = new();
    private readonly ITestOutputHelper output;

    public CloudDetectorTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private CloudDetector Detector() => new(() => settings, StarCatalogue.LoadEmbedded());

    private CloudDetection Assess(GrayFrame frame, RegionOfInterest roi, ClearSkyReference? reference = null, MoonState? moon = null)
    {
        CloudDetection d = Detector().Assess(frame, roi, new WorkingAreaDefinition(), SyntheticCloudSky.Lens, SyntheticSky.Site, SyntheticSky.FrameUtc, moon ?? MoonDown, reference);
        output.WriteLine(d.Assessment.Summary);
        return d;
    }

    private ClearSkyReference Reference()
    {
        settings.Tuned = true;
        ClearSkyReference? r = Assess(Clear, SyntheticCloudSky.WholeArea()).CapturedReference;
        settings.Tuned = false;
        return r ?? throw new InvalidOperationException("No reference captured");
    }

    [Fact]
    public void ClearSkyFindsTheExpectedStars()
    {
        CloudAssessment a = Assess(Clear, SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh)).Assessment;

        Assert.False(a.CloudPresent);
        Assert.False(a.StarTestInconclusive);
        Assert.True(a.ExpectedStars >= 8, $"{a.ExpectedStars} stars expected");
        Assert.True(a.StarFraction >= 0.9, $"fraction {a.StarFraction}");
        Assert.StartsWith("Clear: ", a.Summary);
        Assert.Equal(a.ExpectedStars, a.Expected.Count);
        Assert.Equal(a.DetectedStars, a.Found.Count(f => f));
    }

    [Fact]
    public void ThickCloudOverTheRoiHidesTheStars()
    {
        GrayFrame cloudy = SyntheticCloudSky.Frame(cloudCentre: SyntheticCloudSky.SouthHigh);
        CloudAssessment a = Assess(cloudy, SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh)).Assessment;

        Assert.True(a.CloudPresent);
        Assert.True(a.StarFraction < 0.7);
        Assert.StartsWith("Cloud: ", a.Summary);
    }

    [Fact]
    public void CloudElsewhereLeavesTheRoiClear()
    {
        GrayFrame cloudy = SyntheticCloudSky.Frame(cloudCentre: new HorizontalCoordinate(60, 90), cloudRadiusDeg: 15);
        Assert.False(Assess(cloudy, SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh)).Assessment.CloudPresent);
    }

    [Fact]
    public void TooFewStarsAndNoReferenceIsUnsafe()
    {
        settings.MinExpectedStars = 500;
        CloudAssessment a = Assess(Clear, SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh)).Assessment;

        Assert.True(a.StarTestInconclusive);
        Assert.True(a.BrightnessTestSkipped);
        Assert.True(a.CloudPresent);
        Assert.StartsWith("Cloud unknown: only ", a.Summary);
    }

    [Fact]
    public void ReferenceIsCapturedOnlyWhenTunedClearAndMoonDown()
    {
        RegionOfInterest roi = SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh);
        Assert.Null(Assess(Clear, roi).CapturedReference); // not tuned

        settings.Tuned = true;
        Assert.Null(Assess(Clear, roi, moon: new MoonState(new HorizontalCoordinate(20, 90), 0.5)).CapturedReference);
        Assert.Null(Assess(SyntheticCloudSky.Frame(cloudCentre: new HorizontalCoordinate(60, 90), cloudRadiusDeg: 40), roi).CapturedReference);

        ClearSkyReference? r = Assess(Clear, roi).CapturedReference;
        Assert.NotNull(r);
        Assert.Equal(SyntheticSky.FrameUtc, r!.CapturedUtc);
        Assert.Equal(SyntheticCloudSky.Size, r.Blocks.FrameWidth);
    }

    [Fact]
    public void ReferenceIsNotRecapturedWhenOneExists()
    {
        ClearSkyReference r = Reference();
        settings.Tuned = true;
        Assert.Null(Assess(Clear, SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh), r).CapturedReference);
    }

    [Fact]
    public void ThinCloudWithStarsShowingIsFoundByTheBrightnessTest()
    {
        ClearSkyReference r = Reference();
        GrayFrame thin = SyntheticCloudSky.Frame(cloudCentre: SyntheticCloudSky.SouthHigh, thick: false, cloudBrightness: 1.6, texture: 0.02);
        CloudAssessment a = Assess(thin, SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh), r).Assessment;

        Assert.False(a.BrightnessTestSkipped);
        Assert.True(a.StarFraction >= 0.7, $"fraction {a.StarFraction}");
        Assert.True(a.RelativeBrightness > 1.3, $"relative brightness {a.RelativeBrightness}");
        Assert.True(a.CloudPresent);
        Assert.Contains("brighter than clear sky", a.Summary);
    }

    [Theory]
    [InlineData(0.6)]
    [InlineData(1.4)]
    public void ExposureChangeIsNotCloud(double exposure)
    {
        ClearSkyReference r = Reference();
        CloudAssessment a = Assess(SyntheticCloudSky.Frame(exposure, seed: 9), SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh), r).Assessment;

        Assert.False(a.BrightnessTestSkipped);
        Assert.InRange(a.RelativeBrightness!.Value, 0.9, 1.1);
        Assert.InRange(a.UniformityRatio!.Value, 0.5, 1.5);
        Assert.False(a.CloudPresent);
    }

    [Fact]
    public void BrightnessTestAloneDecidesWhenTooFewStarsAreExpected()
    {
        ClearSkyReference r = Reference();
        settings.MinExpectedStars = 500;
        RegionOfInterest roi = SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh);

        Assert.False(Assess(Clear, roi, r).Assessment.CloudPresent);
        CloudAssessment cloudy = Assess(SyntheticCloudSky.Frame(cloudCentre: SyntheticCloudSky.SouthHigh), roi, r).Assessment;
        Assert.True(cloudy.StarTestInconclusive);
        Assert.True(cloudy.CloudPresent);
    }

    [Fact]
    public void StarsNearTheMoonAreNotExpected()
    {
        RegionOfInterest roi = SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh);
        int withoutMoon = Assess(Clear, roi).Assessment.ExpectedStars;
        var moon = new MoonState(SyntheticCloudSky.SouthHigh, 0.9);
        CloudAssessment a = Assess(Clear, roi, moon: moon).Assessment;

        Assert.True(a.ExpectedStars < withoutMoon);
        Assert.Same(moon, a.Moon);
    }

    [Fact]
    public void ToleranceFollowsTheLensFit()
    {
        Assert.Equal(4.0, CloudDetector.MatchTolerancePx(new LensCalibration { RmsErrorPx = 0.8 }, 2500));
        Assert.Equal(6.0, CloudDetector.MatchTolerancePx(new LensCalibration { RmsErrorPx = 2.0 }, 2500));
        Assert.Equal(10.0, CloudDetector.MatchTolerancePx(new LensCalibration { Manual = true }, 2500));
    }
}
