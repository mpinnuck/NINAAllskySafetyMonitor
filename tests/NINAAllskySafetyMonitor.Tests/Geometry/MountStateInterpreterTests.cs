using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

[Requirement("REQ-07", "REQ-26")]
public sealed class MountStateInterpreterTests
{
    private readonly MountStateInterpreter interpreter = new();
    private static readonly MountState Good = FakeMountStateSource.Tracking(5.0, -40.0);

    [Fact]
    public void TrackingMountWithAPositionIsUsable()
    {
        Assert.Null(interpreter.WhyUnusable(Good));
        Assert.True(interpreter.IsUsableForRoi(Good));
    }

    [Fact]
    public void NoMountInformationIsUnavailableNotAnError() =>
        Assert.Equal("No mount information", interpreter.WhyUnusable(null));

    [Fact]
    public void DisconnectedMount() => Assert.Equal("Mount not connected", interpreter.WhyUnusable(Good with { Connected = false }));

    [Fact]
    public void ParkedMount() => Assert.Equal("Mount parked", interpreter.WhyUnusable(Good with { AtPark = true }));

    [Fact]
    public void MountNotTracking() => Assert.Equal("Mount not tracking", interpreter.WhyUnusable(Good with { Tracking = false }));

    [Theory]
    [InlineData(null, -40.0)]
    [InlineData(5.0, null)]
    [InlineData(double.NaN, -40.0)]
    [InlineData(25.0, -40.0)]
    [InlineData(5.0, -95.0)]
    public void InvalidPositionIsUnavailable(double? ra, double? dec) =>
        Assert.Equal("Mount position unavailable", interpreter.WhyUnusable(Good with { RaHours = ra, DecDeg = dec }));
}
