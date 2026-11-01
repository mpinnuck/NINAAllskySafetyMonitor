using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Acquisition;

[Requirement("REQ-15", "REQ-18", "REQ-50")]
public sealed class ImageFreshnessEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly ImageSourceSettings settings = new();
    private readonly ImageFreshnessEvaluator evaluator = new();

    private static SkyImage CapturedMinutesAgo(double minutes) =>
        new(new byte[] { 1 }, Now.AddMinutes(-minutes), ImageSourceKind.Direct);

    [Fact]
    public void NoImageIsUnsafe()
    {
        ConditionCheck c = evaluator.Evaluate(null, null, null, Now, settings);
        Assert.False(c.Passed);
        Assert.Equal(SafetyCondition.FreshImage, c.Condition);
        Assert.Contains("No image", c.Reason);
    }

    [Fact]
    public void NoImageReasonIncludesTheError()
    {
        ConditionCheck c = evaluator.Evaluate(null, null, "Host key mismatch", Now, settings);
        Assert.False(c.Passed);
        Assert.Contains("Host key mismatch", c.Reason);
    }

    [Fact]
    public void RecentImageIsFresh()
    {
        ConditionCheck c = evaluator.Evaluate(CapturedMinutesAgo(1), Now, null, Now, settings);
        Assert.True(c.Passed);
    }

    [Fact]
    public void ImageOlderThanLimitIsStale()
    {
        ConditionCheck c = evaluator.Evaluate(CapturedMinutesAgo(10.5), Now, null, Now, settings);
        Assert.False(c.Passed);
        Assert.Contains("10.5 min old", c.Reason);
    }

    [Fact]
    public void ShortFetchOutageWithFreshImageStaysFresh()
    {
        ConditionCheck c = evaluator.Evaluate(CapturedMinutesAgo(3), Now.AddMinutes(-3), "Timed out", Now, settings);
        Assert.True(c.Passed);
        Assert.Contains("Timed out", c.Reason);
    }

    [Fact]
    public void NoSuccessfulFetchForTenMinutesIsUnsafe()
    {
        // Image captured recently by the Pi clock, but the PC hasn't reached the Pi for 11 minutes.
        ConditionCheck c = evaluator.Evaluate(CapturedMinutesAgo(1), Now.AddMinutes(-11), "Could not connect", Now, settings);
        Assert.False(c.Passed);
        Assert.Contains("No image fetched for 11.0 min", c.Reason);
    }

    [Fact]
    public void PiClockSlightlyAheadCountsAsZeroAge()
    {
        ConditionCheck c = evaluator.Evaluate(CapturedMinutesAgo(-0.5), Now, null, Now, settings);
        Assert.True(c.Passed);
        Assert.Contains("0.0 min old", c.Reason);
    }
}
