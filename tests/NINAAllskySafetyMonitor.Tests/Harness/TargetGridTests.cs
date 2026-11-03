using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Harness;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Harness;

[Requirement("REQ-42")]
public sealed class TargetGridTests
{
    [Fact]
    public void DefaultGridIsEveryHourAndTenDegrees()
    {
        var grid = new TargetGrid(new ReplayOptions());
        Assert.Equal(240, grid.Targets.Count);
        Assert.Contains(grid.Targets, t => t.Id == "RA05 Dec-35" && t.RaHours == 5 && t.DecDeg == -35);
        Assert.Equal(10, grid.Targets.Select(t => t.DecDeg).Distinct().Count());
    }

    [Fact]
    public void OnlyTargetsMostlyInsideTheWorkingAreaAreJudged()
    {
        var settings = new MonitorSettings();
        var area = new WorkingArea(settings.Calibration.WorkingArea, new LensProjection(SyntheticCloudSky.Lens));
        var grid = new TargetGrid(new ReplayOptions());

        IReadOnlyList<(ReplayTarget Target, RegionOfInterest Roi)> rois = grid.RoisAt(SyntheticSky.FrameUtc, SyntheticSky.Site, settings, area);

        Assert.InRange(rois.Count, 10, 120);
        Assert.All(rois, r => Assert.False(r.Roi.UsesWholeWorkingArea));
        Assert.All(rois, r => Assert.True(r.Roi.InsideFraction >= 0.5));
        Assert.All(rois, r => Assert.InRange(SkyGeometry.SeparationDeg(r.Roi.Centre!.Value, settings.Calibration.WorkingArea.Centre), 0, 75));
    }
}
