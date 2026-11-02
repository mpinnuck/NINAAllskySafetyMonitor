using NINAAllskySafetyMonitor.Core.Decision;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Decision;

[Requirement("REQ-27")]
public sealed class SlewHoldTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeClock clock = new(T0);
    private SafetyDecisionEngine Engine() => new(clock, () => TimeSpan.FromMinutes(4), new SlewHoldPolicy());

    private static readonly MountState Tracking = FakeMountStateSource.Tracking(5, -40);
    private static readonly MountState Slewing = FakeMountStateSource.Tracking(5, -40, slewing: true);

    private static MonitorSnapshot Snapshot(MountState? mount, params ConditionCheck[] checks) =>
        new(null, null, null, null, new SafetyConditions(checks),
            new SafetyStatus(SafetyState.Unsafe, false, string.Join(" · ", checks.Where(c => !c.Passed).Select(c => c.Reason)), null, OverrideMode.Auto, true, T0),
            mount);

    private static MonitorSnapshot AllClear(MountState? mount) => Snapshot(mount,
        new ConditionCheck(SafetyCondition.ActiveWindow, true, "ok"), new ConditionCheck(SafetyCondition.FreshImage, true, "ok"), new ConditionCheck(SafetyCondition.NoCloud, true, "ok"));

    private static MonitorSnapshot Rain(MountState? mount) => Snapshot(mount,
        new ConditionCheck(SafetyCondition.ActiveWindow, true, "ok"), new ConditionCheck(SafetyCondition.NoRain, false, "Rain 0.4 mm/h"));

    [Fact]
    public void PolicyHoldsOnlyWhileAConnectedMountSlews()
    {
        var p = new SlewHoldPolicy();
        Assert.True(p.ShouldHold(Slewing));
        Assert.False(p.ShouldHold(Tracking));
        Assert.False(p.ShouldHold(Slewing with { Connected = false }));
        Assert.False(p.ShouldHold(null));
    }

    [Fact]
    public void SafeIsHeldWhileSlewingEvenIfTheViewFails()
    {
        SafetyDecisionEngine e = Engine();
        e.Decide(AllClear(Tracking));
        clock.UtcNow = T0.AddMinutes(5);
        Assert.True(e.Decide(AllClear(Tracking)).IsSafe);

        clock.UtcNow = T0.AddMinutes(6);
        SafetyStatus held = e.Decide(Rain(Slewing));
        Assert.True(held.IsSafe);
        Assert.Equal(SafetyState.Safe, held.State);
        Assert.Equal("Mount slewing: holding last decision (All conditions clear)", held.Reason);
        Assert.Equal(T0.AddMinutes(6), held.TimestampUtc);

        clock.UtcNow = T0.AddMinutes(7);
        Assert.False(e.Decide(Rain(Tracking)).IsSafe); // after the slew the new view decides
    }

    [Fact]
    public void UnsafeIsHeldWhileSlewingEvenIfTheViewIsClear()
    {
        SafetyDecisionEngine e = Engine();
        e.Decide(Rain(Tracking));
        SafetyStatus held = e.Decide(AllClear(Slewing));
        Assert.False(held.IsSafe);
        Assert.StartsWith("Mount slewing: holding last decision (Rain", held.Reason);
    }

    [Fact]
    public void SlewingBeforeAnyDecisionIsUnsafe()
    {
        SafetyStatus s = Engine().Decide(AllClear(Slewing));
        Assert.False(s.IsSafe);
        Assert.Equal(SafetyState.Unsafe, s.State);
        Assert.Equal("Mount slewing", s.Reason);
    }

    [Fact]
    public void ClearPeriodTimerIsNotRestartedBySlewing()
    {
        SafetyDecisionEngine e = Engine();
        e.Decide(AllClear(Tracking));                 // countdown starts at T0
        clock.UtcNow = T0.AddMinutes(2);
        e.Decide(AllClear(Slewing));                  // held
        clock.UtcNow = T0.AddMinutes(3);
        Assert.Equal(60, e.Decide(AllClear(Tracking)).SecondsToSafe);
    }
}
