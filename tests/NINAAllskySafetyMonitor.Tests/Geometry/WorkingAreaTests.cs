using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

[Requirement("REQ-22", "REQ-25")]
public sealed class WorkingAreaTests
{
    private static readonly WorkingAreaDefinition Default = new();

    [Fact]
    public void DefaultIsSixtyDegreesAroundFifteenDegreesSouthOfTheZenith()
    {
        Assert.Equal(75.0, Default.CentreAltDeg);
        Assert.Equal(180.0, Default.CentreAzDeg);
        Assert.Equal(60.0, Default.RadiusDeg);
        Assert.Empty(Default.Outline);
    }

    [Theory]
    [InlineData(90, 0, true)]     // zenith
    [InlineData(20, 180, true)]   // low in the south: 55° from the centre
    [InlineData(10, 180, false)]  // 65° from the centre
    [InlineData(40, 0, false)]    // north, 65° away over the zenith
    [InlineData(-5, 180, false)]  // below the horizon
    public void ContainsPointsInsideTheCircleAboveTheHorizon(double alt, double az, bool expected) =>
        Assert.Equal(expected, new WorkingArea(Default, null).Contains(new(alt, az)));

    [Fact]
    public void OutlineFurtherReducesTheArea()
    {
        var projection = new LensProjection(LensProjection.Approximate(2000, 2000));
        // Square around the image centre covering roughly the top 45° of sky.
        var outline = new[] { new PixelPoint(500, 500), new PixelPoint(1500, 500), new PixelPoint(1500, 1500), new PixelPoint(500, 1500) };
        var area = new WorkingArea(Default with { Outline = outline }, projection);

        Assert.True(area.Contains(new(85, 180)));
        Assert.False(area.Contains(new(25, 180))); // inside the circle, outside the outline
    }

    [Fact]
    public void OutlineIsIgnoredWithoutALensModel()
    {
        var outline = new[] { new PixelPoint(0, 0), new PixelPoint(1, 0), new PixelPoint(1, 1) };
        Assert.True(new WorkingArea(Default with { Outline = outline }, null).Contains(new(25, 180)));
    }

    [Fact]
    public void PolygonTestUsesEvenOddRule()
    {
        var square = new[] { new PixelPoint(0, 0), new PixelPoint(10, 0), new PixelPoint(10, 10), new PixelPoint(0, 10) };
        Assert.True(WorkingArea.InsidePolygon(new PixelPoint(5, 5), square));
        Assert.False(WorkingArea.InsidePolygon(new PixelPoint(15, 5), square));
    }
}
