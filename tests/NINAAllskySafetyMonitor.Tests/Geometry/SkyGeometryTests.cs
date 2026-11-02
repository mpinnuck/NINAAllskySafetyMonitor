using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

public sealed class SkyGeometryTests
{
    [Fact]
    public void SeparationAlongAzimuthAtTheHorizonIsTheAzimuthDifference() =>
        Assert.Equal(30.0, SkyGeometry.SeparationDeg(new(0, 10), new(0, 40)), 6);

    [Fact]
    public void SeparationFromTheZenithIsTheZenithDistance() =>
        Assert.Equal(25.0, SkyGeometry.SeparationDeg(new(90, 0), new(65, 123)), 6);

    [Fact]
    public void SeparationWrapsAroundNorth() =>
        Assert.Equal(20.0, SkyGeometry.SeparationDeg(new(0, 350), new(0, 10)), 6);

    [Fact]
    public void BearingZeroMovesTowardsTheZenith()
    {
        HorizontalCoordinate p = SkyGeometry.Destination(new(45, 180), 0, 10);
        Assert.Equal(55.0, p.AltDeg, 6);
        Assert.Equal(180.0, p.AzDeg, 6);
    }

    [Fact]
    public void BearingNinetyMovesTowardsIncreasingAzimuth()
    {
        HorizontalCoordinate p = SkyGeometry.Destination(new(45, 180), 90, 10);
        Assert.True(p.AzDeg > 180);
        Assert.Equal(10.0, SkyGeometry.SeparationDeg(new(45, 180), p), 6);
    }

    [Theory]
    [InlineData(-10, 350)]
    [InlineData(370, 10)]
    [InlineData(360, 0)]
    public void NormaliseGivesZeroTo360(double input, double expected) =>
        Assert.Equal(expected, SkyGeometry.Normalise(input), 9);
}
