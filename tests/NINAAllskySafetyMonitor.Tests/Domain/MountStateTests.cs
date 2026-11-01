using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Domain;

[Requirement("REQ-05", "REQ-07")]
public sealed class MountStateTests
{
    private static MountState Mount(double? ra, double? dec) =>
        new(true, false, true, false, ra, dec, CoordinateEpoch.JNow, null, null, DateTime.UtcNow);

    [Fact]
    public void ValidPositionIsRecognised() =>
        Assert.True(Mount(20.142777, -90).HasValidPosition);

    [Theory]
    [InlineData(null, -30.0)]
    [InlineData(5.0, null)]
    [InlineData(double.NaN, -30.0)]
    [InlineData(5.0, double.NaN)]
    [InlineData(24.0, -30.0)]
    [InlineData(-0.1, -30.0)]
    [InlineData(5.0, 90.5)]
    public void MissingOrInvalidPositionIsUnavailable(double? ra, double? dec) =>
        Assert.False(Mount(ra, dec).HasValidPosition);
}
