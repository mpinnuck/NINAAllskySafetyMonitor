using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

[Requirement("REQ-23", "REQ-25", "REQ-26", "REQ-58")]
public sealed class RoiBuilderTests
{
    private readonly RoiBuilder builder = new();
    private readonly WorkingArea area = new(new WorkingAreaDefinition(), null);

    [Fact]
    public void CircleWellInsideTheWorkingAreaIsUsed()
    {
        RegionOfInterest roi = builder.Build(new HorizontalCoordinate(70, 180), null, 15, 0.5, area);

        Assert.False(roi.UsesWholeWorkingArea);
        Assert.Equal(1.0, roi.InsideFraction);
        Assert.Equal(15, roi.RadiusDeg);
        Assert.Equal(string.Empty, roi.Note);
    }

    [Fact]
    public void CircleOnTheEdgeIsAboutHalfInside()
    {
        // Centre on the working-area boundary: 60° from alt 75 / az 180, away from the zenith (alt 15, due south).
        HorizontalCoordinate onEdge = SkyGeometry.Destination(new(75, 180), 180, 60);
        double fraction = RoiBuilder.InsideFraction(onEdge, 10, area);
        Assert.InRange(fraction, 0.4, 0.6);
    }

    [Fact]
    public void MostlyOutsideFallsBackToTheWholeWorkingArea()
    {
        RegionOfInterest roi = builder.Build(new HorizontalCoordinate(30, 0), null, 15, 0.5, area);

        Assert.True(roi.UsesWholeWorkingArea);
        Assert.NotNull(roi.Centre);
        Assert.True(roi.InsideFraction < 0.5);
        Assert.EndsWith("inside the working area: judging the whole working area", roi.Note);
        Assert.StartsWith("ROI ", roi.Note);
    }

    [Fact]
    public void WorkingAreaCentredOnTheZenithContainsARoiAtTheZenith()
    {
        var zenithArea = new WorkingArea(new WorkingAreaDefinition { CentreAltDeg = 90, RadiusDeg = 55 }, null);
        Assert.Equal(1.0, RoiBuilder.InsideFraction(new HorizontalCoordinate(90, 0), 15, zenithArea));
        Assert.InRange(RoiBuilder.InsideFraction(new HorizontalCoordinate(35, 200), 10, zenithArea), 0.4, 0.6);
    }

    [Fact]
    public void ZeroMinimumNeverFallsBack()
    {
        RegionOfInterest roi = builder.Build(new HorizontalCoordinate(40, 0), null, 15, 0.0, area);
        Assert.False(roi.UsesWholeWorkingArea);
    }

    [Fact]
    public void NoCentreJudgesTheWholeWorkingAreaAndSaysWhy()
    {
        RegionOfInterest roi = builder.Build(null, "Mount parked", 15, 0.5, area);

        Assert.True(roi.UsesWholeWorkingArea);
        Assert.Null(roi.Centre);
        Assert.Equal("Mount parked: judging the whole working area", roi.Note);
    }
}
