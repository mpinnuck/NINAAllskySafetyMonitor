using NINAAllskySafetyMonitor.Core.Decision;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Decision;

[Requirement("REQ-03", "REQ-37", "REQ-38", "REQ-39", "REQ-50")]
public sealed class SafetyDecisionEngineTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeClock clock = new(T0);
    private bool undeterminedIsSafe;

    private SafetyDecisionEngine Engine() => new(clock, () => TimeSpan.FromMinutes(4), null, () => undeterminedIsSafe);

    private static MonitorSnapshot StaleImage(ConditionCheck? extra = null) => Snapshot(SafetyState.Unsafe, new[]
    {
        Pass(SafetyCondition.ActiveWindow), ConditionCheck.Unknown(SafetyCondition.FreshImage, "Image is 12.0 min old (limit 10 min)"),
        ConditionCheck.Unknown(SafetyCondition.NoCloud, "No image to judge cloud"),
        Pass(SafetyCondition.NoRain), Pass(SafetyCondition.WindBelowLimit), Pass(SafetyCondition.WeatherCurrent),
    }.Select(c => extra is not null && c.Condition == extra.Condition ? extra : c).ToArray());

    [Fact]
    public void UndeterminedIsUnsafeByDefault()
    {
        SafetyStatus s = Engine().Decide(StaleImage());
        Assert.False(s.IsSafe);
        Assert.False(s.Assumed);
        Assert.Null(s.SecondsToSafe);
    }

    [Fact]
    public void UndeterminedCanBeAssumedSafeAfterTheClearPeriod()
    {
        undeterminedIsSafe = true;
        SafetyDecisionEngine e = Engine();
        SafetyStatus first = e.Decide(StaleImage());
        Assert.False(first.IsSafe);
        Assert.True(first.Assumed);
        Assert.Equal("Assumed safe: Image is 12.0 min old (limit 10 min) · No image to judge cloud: safe in 4:00", first.Reason);

        clock.UtcNow = T0.AddMinutes(4);
        SafetyStatus safe = e.Decide(StaleImage());
        Assert.True(safe.IsSafe);
        Assert.True(safe.Assumed);
        Assert.Equal(SafetyState.Safe, safe.State);
        Assert.StartsWith("Assumed safe: ", safe.Reason);
    }

    [Fact]
    public void DetectedCloudIsUnsafeEvenWhenUndeterminedIsSafe()
    {
        undeterminedIsSafe = true;
        SafetyStatus s = Engine().Decide(StaleImage(new ConditionCheck(SafetyCondition.NoCloud, false, "Cloud: 2 of 11 stars (18%)")));
        Assert.False(s.IsSafe);
        Assert.False(s.Assumed);
        Assert.Null(s.SecondsToSafe);
    }

    [Fact]
    public void MissingWeatherCanBeAssumedSafe()
    {
        undeterminedIsSafe = true;
        SafetyStatus s = Engine().Decide(StaleImage(ConditionCheck.Unknown(SafetyCondition.WeatherCurrent, "No weather data: timeout")));
        Assert.True(s.Assumed);
        Assert.Equal(240, s.SecondsToSafe);
    }

    [Fact]
    public void ReportedRainIsUnsafeEvenWhenUndeterminedIsSafe()
    {
        undeterminedIsSafe = true;
        SafetyStatus s = Engine().Decide(StaleImage(new ConditionCheck(SafetyCondition.NoRain, false, "Rain 0.4 mm/h")));
        Assert.False(s.IsSafe);
        Assert.False(s.Assumed);
        Assert.Null(s.SecondsToSafe);
    }

    [Fact]
    public void ClearConditionsAreNotMarkedAssumed()
    {
        undeterminedIsSafe = true;
        SafetyDecisionEngine e = Engine();
        e.Decide(AllClear());
        clock.UtcNow = T0.AddMinutes(4);
        SafetyStatus s = e.Decide(AllClear());
        Assert.True(s.IsSafe);
        Assert.False(s.Assumed);
        Assert.Equal("All conditions clear", s.Reason);
    }

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
