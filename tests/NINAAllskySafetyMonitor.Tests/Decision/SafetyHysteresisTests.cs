using NINAAllskySafetyMonitor.Core.Decision;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Decision;

[Requirement("REQ-38", "REQ-39")]
public sealed class SafetyHysteresisTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan FourMinutes = TimeSpan.FromMinutes(4);
    private readonly SafetyHysteresis h = new();

    [Fact]
    public void SafeOnlyAfterFourContinuousClearMinutes()
    {
        Assert.Equal((false, (int?)240), h.Apply(true, T0, FourMinutes));
        Assert.Equal((false, (int?)60), h.Apply(true, T0.AddMinutes(3), FourMinutes));
        Assert.Equal((true, (int?)null), h.Apply(true, T0.AddMinutes(4), FourMinutes));
    }

    [Fact]
    public void AnyFailureIsUnsafeAtOnceAndRestartsTheClearPeriod()
    {
        h.Apply(true, T0, FourMinutes);
        Assert.True(h.Apply(true, T0.AddMinutes(5), FourMinutes).Safe);

        Assert.Equal((false, (int?)null), h.Apply(false, T0.AddMinutes(6), FourMinutes));
        Assert.Equal((false, (int?)240), h.Apply(true, T0.AddMinutes(7), FourMinutes));
        Assert.False(h.Apply(true, T0.AddMinutes(10), FourMinutes).Safe);
        Assert.True(h.Apply(true, T0.AddMinutes(11), FourMinutes).Safe);
    }

    [Fact]
    public void ZeroDelayIsSafeAtOnce() => Assert.True(h.Apply(true, T0, TimeSpan.Zero).Safe);

    [Fact]
    public void ResetRestartsTheClearPeriod()
    {
        h.Apply(true, T0, FourMinutes);
        h.Reset();
        Assert.Null(h.ClearSinceUtc);
        Assert.Equal((false, (int?)240), h.Apply(true, T0.AddMinutes(10), FourMinutes));
    }
}
