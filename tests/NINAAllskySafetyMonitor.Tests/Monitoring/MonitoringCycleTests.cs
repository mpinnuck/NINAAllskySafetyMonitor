using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-10", "REQ-37")]
public sealed class MonitoringCycleTests
{
    private readonly MonitorSettings settings = new();
    private readonly FakeAstroCalculator astro = new();

    private MonitoringCycle Cycle() => new(
        () => settings,
        new FakeClock(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc)),
        new SiteLocationResolver(new FakeSiteLocationSource(), () => settings.Site),
        new OperatingWindow(astro));

    [Fact]
    public async Task IdleAndUnsafeOutsideWindow()
    {
        astro.SunAltitude = 10;
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);
        Assert.Equal(SafetyState.Idle, s.Status.State);
        Assert.False(s.Status.IsSafe);
        Assert.Null(s.Image);
    }

    [Fact]
    public async Task UnsafeInsideWindowUntilImageChecksExist()
    {
        astro.SunAltitude = -30;
        MonitorSnapshot s = await Cycle().RunOnceAsync(CancellationToken.None);
        Assert.Equal(SafetyState.Unsafe, s.Status.State);
        Assert.False(s.Status.IsSafe);
        Assert.False(s.Conditions.AllPassed);
    }

    [Fact]
    public async Task SettingsChangesApplyOnNextCycle()
    {
        astro.SunAltitude = 10;
        MonitoringCycle cycle = Cycle();
        Assert.Equal(SafetyState.Idle, (await cycle.RunOnceAsync(CancellationToken.None)).Status.State);

        settings.Window.SunAltitudeLimitDeg = 90;
        Assert.Equal(SafetyState.Unsafe, (await cycle.RunOnceAsync(CancellationToken.None)).Status.State);
    }
}
