using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

[Requirement("REQ-08", "REQ-09", "REQ-10")]
public sealed class OperatingWindowTests
{
    private static readonly SiteLocation Site = new(-33.7, 151.1, 134);

    private static ConditionCheck Evaluate(double sun, double limit) =>
        new OperatingWindow(new FakeAstroCalculator { SunAltitude = sun }).Evaluate(Site, DateTime.UtcNow, limit);

    [Fact]
    public void ActiveWhenSunBelowLimit()
    {
        ConditionCheck c = Evaluate(-25, -18);
        Assert.True(c.Passed);
        Assert.Equal(SafetyCondition.ActiveWindow, c.Condition);
    }

    [Theory]
    [InlineData(-10.0)]
    [InlineData(-18.0)]
    [InlineData(45.0)]
    public void InactiveWhenSunAtOrAboveLimit(double sun)
    {
        ConditionCheck c = Evaluate(sun, -18);
        Assert.False(c.Passed);
        Assert.Contains("Outside operating window", c.Reason);
    }

    [Fact]
    public void DaytimeTestingWithRaisedLimit() =>
        Assert.True(Evaluate(45, 90).Passed);
}
