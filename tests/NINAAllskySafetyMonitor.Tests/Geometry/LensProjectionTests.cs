using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

[Requirement("REQ-19", "REQ-24")]
public sealed class LensProjectionTests
{
    private static LensProjection Approximate() => new(LensProjection.Approximate(3000, 2000));

    [Fact]
    public void ApproximateModelPutsTheZenithAtTheCentreAndTheHorizonOnTheInscribedCircle()
    {
        LensProjection p = Approximate();
        Assert.Equal(new PixelPoint(1500, 1000), p.ToPixel(new(90, 0)));
        Assert.Equal(1000.0, p.HorizonRadiusPx, 6);
        Assert.True(p.Calibration.Manual);
    }

    [Fact]
    public void ApproximateModelHasNorthUpAndEastLeft()
    {
        LensProjection p = Approximate();
        PixelPoint north = p.ToPixel(new(0, 0));
        PixelPoint east = p.ToPixel(new(0, 90));
        PixelPoint south = p.ToPixel(new(0, 180));
        Assert.Equal(1500.0, north.X, 6);
        Assert.Equal(0.0, north.Y, 6);
        Assert.Equal(500.0, east.X, 6);
        Assert.Equal(1000.0, east.Y, 6);
        Assert.Equal(2000.0, south.Y, 6);
    }

    [Fact]
    public void EastClockwiseAndRotationTurnTheImage()
    {
        var p = new LensProjection(LensProjection.FromHorizonRadius(1000, 1000, 900, 90, eastIsClockwise: true));
        PixelPoint north = p.ToPixel(new(0, 0));
        Assert.Equal(1900.0, north.X, 6); // north rotated to image right
        Assert.Equal(1000.0, north.Y, 6);
        PixelPoint east = p.ToPixel(new(0, 90));
        Assert.Equal(1000.0, east.X, 6); // then east is down
        Assert.Equal(1900.0, east.Y, 6);
    }

    [Theory]
    [InlineData(75, 180)]
    [InlineData(30, 45)]
    [InlineData(10, 300)]
    [InlineData(60, 0.5)]
    public void PixelToHorizontalReversesHorizontalToPixel(double alt, double az)
    {
        var lens = new LensCalibration { CentreX = 1520, CentreY = 1010, NorthRotationDeg = 12, EastIsClockwise = false, RadialCoefficients = new[] { 640.0, -20.0, 4.0 } };
        var p = new LensProjection(lens);
        HorizontalCoordinate back = p.ToHorizontal(p.ToPixel(new(alt, az)));
        Assert.Equal(alt, back.AltDeg, 6);
        Assert.Equal(0.0, SkyGeometry.SeparationDeg(new(alt, az), back), 5);
    }

    [Fact]
    public void ModelWithoutAPositiveRadiusIsRejected() =>
        Assert.Throws<ArgumentException>(() => new LensProjection(new LensCalibration()));
}
