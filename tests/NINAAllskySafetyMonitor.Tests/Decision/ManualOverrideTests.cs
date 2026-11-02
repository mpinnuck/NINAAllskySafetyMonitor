using NINAAllskySafetyMonitor.Core.Decision;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Decision;

[Requirement("REQ-40")]
public sealed class ManualOverrideTests
{
    private readonly ListLogSink log = new();
    private static readonly SafetyStatus Unsafe =
        new(SafetyState.Unsafe, false, "Rain 1.0 mm/h", null, OverrideMode.Auto, false, DateTime.UtcNow);

    [Fact]
    public void StartsInAutoAndChangesNothing()
    {
        var o = new ManualOverride(log);
        Assert.Equal(OverrideMode.Auto, o.Mode);
        Assert.Same(Unsafe, o.Apply(Unsafe));
    }

    [Fact]
    public void ForceSafeReportsSafeAndSaysWhy()
    {
        var o = new ManualOverride(log) { Mode = OverrideMode.ForceSafe };
        SafetyStatus s = o.Apply(Unsafe);
        Assert.True(s.IsSafe);
        Assert.Equal(SafetyState.Safe, s.State);
        Assert.Equal(OverrideMode.ForceSafe, s.Override);
        Assert.Equal("MANUAL OVERRIDE: Force Safe (monitor says Unsafe: Rain 1.0 mm/h)", s.Reason);
    }

    [Fact]
    public void ForceUnsafeReportsUnsafe()
    {
        var safe = Unsafe with { State = SafetyState.Safe, IsSafe = true, Reason = "All conditions clear" };
        SafetyStatus s = new ManualOverride(log) { Mode = OverrideMode.ForceUnsafe }.Apply(safe);
        Assert.False(s.IsSafe);
        Assert.StartsWith("MANUAL OVERRIDE: Force Unsafe", s.Reason);
    }

    [Fact]
    public void EveryChangeIsLogged()
    {
        var o = new ManualOverride(log);
        int changes = 0;
        o.Changed += (_, _) => changes++;

        o.Mode = OverrideMode.ForceSafe;
        o.Mode = OverrideMode.ForceSafe;
        o.Mode = OverrideMode.Auto;

        Assert.Equal(2, changes);
        Assert.Equal(new[] { "Manual override set to Force Safe", "Manual override set to Auto" }, log.Entries.Select(e => e.Message));
    }
}
