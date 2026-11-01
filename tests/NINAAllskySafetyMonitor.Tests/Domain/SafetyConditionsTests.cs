using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Domain;

[Requirement("REQ-37")]
public sealed class SafetyConditionsTests
{
    private static ConditionCheck Check(SafetyCondition c, bool passed) => new(c, passed, c.ToString());

    [Fact]
    public void AllPassedWhenEveryCheckPasses()
    {
        var conditions = new SafetyConditions(Enum.GetValues<SafetyCondition>().Select(c => Check(c, true)).ToList());
        Assert.True(conditions.AllPassed);
        Assert.Empty(conditions.Failures);
    }

    [Fact]
    public void NotPassedWhenAnyCheckFails()
    {
        var checks = Enum.GetValues<SafetyCondition>().Select(c => Check(c, c != SafetyCondition.NoCloud)).ToList();
        var conditions = new SafetyConditions(checks);
        Assert.False(conditions.AllPassed);
        Assert.Equal(SafetyCondition.NoCloud, Assert.Single(conditions.Failures).Condition);
    }

    [Fact]
    public void NoChecksIsNotSafe() =>
        Assert.False(new SafetyConditions(Array.Empty<ConditionCheck>()).AllPassed);
}
