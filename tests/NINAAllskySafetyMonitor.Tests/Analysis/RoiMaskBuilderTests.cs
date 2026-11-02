using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Analysis;

[Requirement("REQ-25", "REQ-29")]
public sealed class RoiMaskBuilderTests
{
    private const int Size = SyntheticCloudSky.Size;
    private static readonly LensProjection Projection = new(SyntheticCloudSky.Lens);

    private static WorkingArea Area(IReadOnlyList<PixelPoint>? outline = null) =>
        new(new WorkingAreaDefinition { Outline = outline ?? Array.Empty<PixelPoint>() }, Projection);

    private static bool In(PixelMask m, HorizontalCoordinate h)
    {
        PixelPoint p = Projection.ToPixel(h);
        return m.Contains((int)p.X, (int)p.Y);
    }

    [Fact]
    public void RoiMaskIsTheCircleWithinTheWorkingArea()
    {
        var builder = new RoiMaskBuilder();
        PixelMask m = builder.Build(SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh), Area(), Projection, Size, Size);

        Assert.True(In(m, SyntheticCloudSky.SouthHigh));
        Assert.True(In(m, new HorizontalCoordinate(58, 180)));   // 12° from the centre
        Assert.False(In(m, new HorizontalCoordinate(50, 180)));  // 20° away
        Assert.False(In(m, new HorizontalCoordinate(70, 90)));
        Assert.True(m.Count < builder.BuildArea(Area(), Projection, Size, Size).Count / 5);
    }

    [Fact]
    public void WholeAreaRoiUsesTheWorkingAreaMask()
    {
        var builder = new RoiMaskBuilder();
        PixelMask area = builder.BuildArea(Area(), Projection, Size, Size);
        Assert.Same(area, builder.Build(SyntheticCloudSky.WholeArea(), Area(), Projection, Size, Size));
        Assert.True(In(area, new HorizontalCoordinate(30, 180)));  // 45° from the area centre
        Assert.False(In(area, new HorizontalCoordinate(20, 0)));   // low in the north, outside
    }

    [Fact]
    public void OutlineCutsTheWorkingArea()
    {
        PixelPoint c = Projection.ToPixel(SyntheticCloudSky.SouthHigh);
        var halfOnly = new[] { new PixelPoint(0, 0), new PixelPoint(c.X, 0), new PixelPoint(c.X, Size), new PixelPoint(0, Size) };
        PixelMask m = new RoiMaskBuilder().BuildArea(Area(halfOnly), Projection, Size, Size);

        Assert.True(m.Contains((int)c.X - 20, (int)c.Y));
        Assert.False(m.Contains((int)c.X + 20, (int)c.Y));
    }

    [Fact]
    public void AreaMaskIsKeptUntilSomethingChanges()
    {
        var builder = new RoiMaskBuilder();
        PixelMask first = builder.BuildArea(Area(), Projection, Size, Size);
        Assert.Same(first, builder.BuildArea(Area(), Projection, Size, Size));
        Assert.NotSame(first, builder.BuildArea(new WorkingArea(new WorkingAreaDefinition { RadiusDeg = 40 }, Projection), Projection, Size, Size));
    }

    [Fact]
    public void MaskBoundsEncloseItsPixels()
    {
        PixelMask m = new RoiMaskBuilder().Build(SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh), Area(), Projection, Size, Size);
        PixelRect b = m.Bounds!.Value;
        PixelPoint c = Projection.ToPixel(SyntheticCloudSky.SouthHigh);
        Assert.InRange(c.X, b.X, b.X + b.Width);
        Assert.InRange(c.Y, b.Y, b.Y + b.Height);
        Assert.InRange(b.Width, 80, 250);
    }
}
