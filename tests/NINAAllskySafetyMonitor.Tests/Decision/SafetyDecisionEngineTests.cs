using NINAAllskySafetyMonitor.Core.Decision;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Decision;

[Requirement("REQ-03", "REQ-37", "REQ-38", "REQ-39")]
public sealed class SafetyDecisionEngineTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeClock clock = new(T0);
    private SafetyDecisionEngine Engine() => new(clock, () => TimeSpan.FromMinutes(4));

    private static MonitorSnapshot Snapshot(SafetyState state, params ConditionCheck[] checks) =>
        new(null, null, null, null, new SafetyConditions(checks),
            new SafetyStatus(state, false, string.Join(" · ", checks.Where(c => !c.Passed).Select(c => c.Reason)), null, OverrideMode.Auto, true, T0));

    private static ConditionCheck Pass(SafetyCondition c) => new(c, true, "ok");

    private static MonitorSnapshot AllClear() => Snapshot(SafetyState.Unsafe,
        Pass(SafetyCondition.ActiveWindow), Pass(SafetyCondition.FreshImage), Pass(SafetyCondition.NoCloud),
        Pass(SafetyCondition.NoRain), Pass(SafetyCondition.WindBelowLimit), Pass(SafetyCondition.WeatherCurrent));

    [Fact]
    public void ClearConditionsCountDownThenBecomeSafe()
    {
        SafetyDecisionEngine e = Engine();
        SafetyStatus first = e.Decide(AllClear());
        Assert.False(first.IsSafe);
        Assert.Equal(240, first.SecondsToSafe);
        Assert.Equal("All conditions clear: safe in 4:00", first.Reason);

        clock.UtcNow = T0.AddSeconds(150);
        Assert.Equal("All conditions clear: safe in 1:30", e.Decide(AllClear()).Reason);

        clock.UtcNow = T0.AddMinutes(4);
        SafetyStatus safe = e.Decide(AllClear());
        Assert.True(safe.IsSafe);
        Assert.Equal(SafetyState.Safe, safe.State);
        Assert.Null(safe.SecondsToSafe);
    }

    [Fact]
    public void FailureIsUnsafeImmediatelyWithItsReason()
    {
        SafetyDecisionEngine e = Engine();
        e.Decide(AllClear());
        clock.UtcNow = T0.AddMinutes(5);
        Assert.True(e.Decide(AllClear()).IsSafe);

        clock.UtcNow = T0.AddMinutes(6);
        SafetyStatus s = e.Decide(Snapshot(SafetyState.Unsafe, Pass(SafetyCondition.ActiveWindow), new ConditionCheck(SafetyCondition.NoRain, false, "Rain 0.4 mm/h")));
        Assert.False(s.IsSafe);
        Assert.Equal("Rain 0.4 mm/h", s.Reason);

        clock.UtcNow = T0.AddMinutes(7);
        Assert.False(e.Decide(AllClear()).IsSafe);
    }

    [Fact]
    public void FailedCycleWithNoConditionsIsUnsafeAndRestartsTheCountdown()
    {
        SafetyDecisionEngine e = Engine();
        e.Decide(AllClear());
        clock.UtcNow = T0.AddMinutes(3);
        SafetyStatus error = e.Decide(Snapshot(SafetyState.Unsafe) with { Status = SafetyStatus.Starting(T0) with { State = SafetyState.Unsafe, Reason = "Monitor error: boom" } });
        Assert.False(error.IsSafe);
        Assert.Equal("Monitor error: boom", error.Reason);

        clock.UtcNow = T0.AddMinutes(4);
        Assert.Equal(240, e.Decide(AllClear()).SecondsToSafe);
    }

    [Fact]
    public void IdleStaysIdleAndRestartsTheCountdown()
    {
        SafetyDecisionEngine e = Engine();
        e.Decide(AllClear());
        SafetyStatus idle = e.Decide(Snapshot(SafetyState.Idle, new ConditionCheck(SafetyCondition.ActiveWindow, false, "Outside operating window")));
        Assert.Equal(SafetyState.Idle, idle.State);
        Assert.False(idle.IsSafe);

        clock.UtcNow = T0.AddMinutes(10);
        Assert.Equal(240, e.Decide(AllClear()).SecondsToSafe);
    }
}
