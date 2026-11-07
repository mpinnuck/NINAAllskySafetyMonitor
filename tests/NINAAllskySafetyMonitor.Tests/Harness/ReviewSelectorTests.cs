using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Harness;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Harness;

[Requirement("REQ-61")]
public sealed class ReviewSelectorTests
{
    private static readonly CloudDetectionSettings Limits = new();
    private static readonly ReplayTarget Target = new("RA5 Dec-35", 5, -35);

    private static FrameResult Frame(DateOnly night, double fraction, double? brightness = null) =>
        new(new SavedFrame("f.jpg", night, DateTime.UnixEpoch), -30, true, null,
            new[] { new TargetResult(Target, new RegionOfInterest(new HorizontalCoordinate(70, 180), 15, false, 1, string.Empty),
                new CloudAssessment(20, (int)(fraction * 20), fraction, brightness, null, false, brightness is null, fraction < 0.7, "x")) },
            false);

    [Fact]
    public void VerdictChangeWithinANightIsPicked()
    {
        var s = new ReviewSelector(0.1);
        DateOnly n = new(2026, 10, 19);
        Assert.Empty(s.Select(Frame(n, 1.0), Limits));
        var picked = s.Select(Frame(n, 0.2), Limits);
        Assert.Equal("Clear → Cloud", Assert.Single(picked).Why);
        Assert.Empty(s.Select(Frame(n, 0.2), Limits));
    }

    [Fact]
    public void ANewNightStartsAfresh()
    {
        var s = new ReviewSelector(0.1);
        s.Select(Frame(new DateOnly(2026, 10, 19), 1.0), Limits);
        Assert.Empty(s.Select(Frame(new DateOnly(2026, 10, 20), 0.2), Limits));
    }

    [Theory]
    [InlineData(0.65, null, "star fraction near limit")]
    [InlineData(0.75, null, "star fraction near limit")]
    [InlineData(1.0, 1.25, "brightness near limit")]
    public void MetricsNearTheirLimitArePicked(double fraction, double? brightness, string why)
    {
        var s = new ReviewSelector(0.1);
        Assert.Contains(why, Assert.Single(s.Select(Frame(new DateOnly(2026, 10, 19), fraction, brightness), Limits)).Why);
    }

    [Fact]
    public void ClearlyClearIsNotPicked() => Assert.Empty(new ReviewSelector(0.1).Select(Frame(new DateOnly(2026, 10, 19), 0.95, 1.0), Limits));

    [Fact]
    public void BrightnessNearMoonAdjustedLimitIsComparedToAdjustedThreshold()
    {
        var target = new TargetResult(Target, new RegionOfInterest(new HorizontalCoordinate(70, 180), 15, false, 1, string.Empty),
            new CloudAssessment(20, 20, 1, 1.25, 1.0, false, false, false, "x")
            {
                Moon = new MoonState(new HorizontalCoordinate(90, 180), 1),
            });
        var frame = new FrameResult(new SavedFrame("f.jpg", new DateOnly(2026, 10, 19), DateTime.UnixEpoch),
            -30, true, null, new[] { target }, false);

        Assert.Empty(new ReviewSelector(0.1).Select(frame, Limits));
    }
}
