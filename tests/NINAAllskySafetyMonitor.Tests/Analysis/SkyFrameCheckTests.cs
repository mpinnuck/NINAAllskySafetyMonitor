using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Analysis;

[Requirement("REQ-60")]
public sealed class SkyFrameCheckTests
{
    private static readonly WorkingArea Area = new(new WorkingAreaDefinition(), new LensProjection(SyntheticCloudSky.Lens));

    [Fact]
    public void SkyFramePasses()
    {
        ConditionCheck c = new SkyFrameCheck().Evaluate(SyntheticCloudSky.Frame(), Area, SyntheticCloudSky.Lens);
        Assert.True(c.Passed);
        Assert.Equal(SafetyCondition.NoCloud, c.Condition);
    }

    [Fact]
    public void CloudyFramePasses() =>
        Assert.True(new SkyFrameCheck().Evaluate(SyntheticCloudSky.Frame(cloudCentre: SyntheticCloudSky.SouthHigh, cloudRadiusDeg: 90), Area, SyntheticCloudSky.Lens).Passed);

    [Fact]
    public void NoticeImageIsNotASkyFrame()
    {
        ConditionCheck c = new SkyFrameCheck().Evaluate(SyntheticCloudSky.NoticeImage(), Area, SyntheticCloudSky.Lens);
        Assert.False(c.Passed);
        Assert.Equal("Not a sky frame (Allsky notice image?)", c.Reason);
    }

    [Fact]
    public void ImageOfAnotherSizeIsNotASkyFrame()
    {
        LensCalibration lens = SyntheticCloudSky.Lens with { ImageWidth = 1000, ImageHeight = 1000 };
        ConditionCheck c = new SkyFrameCheck().Evaluate(SyntheticCloudSky.NoticeImage(800, 600), Area, lens);
        Assert.False(c.Passed);
        Assert.Equal("Not a sky frame: image is 800 × 600, the lens was calibrated on 1000 × 1000", c.Reason);
    }

    [Fact]
    public void SizeIsNotCheckedForAHandEnteredLens() =>
        Assert.True(new SkyFrameCheck().Evaluate(SyntheticCloudSky.Frame(), Area, SyntheticCloudSky.Lens with { ImageWidth = 0 }).Passed);
}
