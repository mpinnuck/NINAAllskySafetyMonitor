using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Monitoring;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Monitoring;

[Requirement("REQ-05", "REQ-27")]
public sealed class MountWatcherTests
{
    private readonly FakeMountStateSource mount = new();
    private readonly ListLogSink log = new();
    private static readonly MountState Tracking = FakeMountStateSource.Tracking(5, -40);

    private MountWatcher Watcher(double pollSeconds = 60) => new(mount, () => TimeSpan.FromSeconds(pollSeconds), log);

    [Fact]
    public void FirstReadIsTheBaselineNotAChange()
    {
        mount.Current = Tracking;
        Assert.False(Watcher().Poll());
    }

    [Fact]
    public void SlewStartAndEndAreChanges()
    {
        MountWatcher w = Watcher();
        var raised = new List<MountState?>();
        w.Changed += (_, m) => raised.Add(m);

        mount.Current = Tracking;
        w.Poll();
        mount.Current = Tracking with { Slewing = true };
        Assert.True(w.Poll());
        mount.Current = Tracking with { RaHours = 8, DecDeg = -60 };
        Assert.True(w.Poll());

        Assert.Equal(2, raised.Count);
        Assert.True(raised[0]!.Slewing);
    }

    [Theory]
    [InlineData(false, false, true)]   // disconnected
    [InlineData(true, true, true)]     // parked
    public void ConnectionAndParkAreChanges(bool connected, bool parked, bool expected)
    {
        MountWatcher w = Watcher();
        mount.Current = Tracking;
        w.Poll();
        mount.Current = Tracking with { Connected = connected, AtPark = parked };
        Assert.Equal(expected, w.Poll());
    }

    [Fact]
    public void TrackingOnOrOffIsAChange()
    {
        MountWatcher w = Watcher();
        mount.Current = Tracking;
        w.Poll();
        mount.Current = Tracking with { Tracking = false };
        Assert.True(w.Poll());
    }

    [Fact]
    public void SmallMovesAreNotChangesButLargeOnesAre()
    {
        MountWatcher w = Watcher();
        mount.Current = Tracking;
        w.Poll();
        mount.Current = Tracking with { DecDeg = -40.3 };
        Assert.False(w.Poll());
        mount.Current = Tracking with { DecDeg = -41.0 };
        Assert.True(w.Poll());
    }

    [Fact]
    public void MountAppearingOrDisappearingIsAChange()
    {
        MountWatcher w = Watcher();
        w.Poll();
        mount.Current = Tracking;
        Assert.True(w.Poll());
        mount.Current = null;
        Assert.True(w.Poll());
    }

    [Fact]
    public async Task RunningWatcherReportsAChangeAndStops()
    {
        mount.Current = Tracking;
        MountWatcher w = Watcher(pollSeconds: 0.02);
        var changed = new TaskCompletionSource<MountState?>();
        w.Changed += (_, m) => changed.TrySetResult(m);

        w.Start();
        await Task.Delay(60);
        mount.Current = Tracking with { Slewing = true };
        MountState? seen = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await w.StopAsync();

        Assert.True(seen!.Slewing);
    }
}
