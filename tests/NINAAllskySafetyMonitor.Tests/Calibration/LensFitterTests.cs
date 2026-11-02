using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Calibration;

[Requirement("REQ-19", "REQ-20")]
public sealed class LensFitterTests
{
    private const int Size = 2500;

    private static LensCalibration RoughStart(LensCalibration truth) => truth with
    {
        CentreX = truth.CentreX + 25,
        CentreY = truth.CentreY - 20,
        NorthRotationDeg = truth.NorthRotationDeg + 2,
        RadialCoefficients = new[] { 970.0 },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoversTheLensFromARoughStart(bool eastClockwise)
    {
        LensCalibration truth = SyntheticSky.MarksLens(eastClockwise);
        List<DetectedStar> spots = SyntheticSky.Detected(truth, Size);

        LensFitResult? fit = new LensFitter().Fit(spots, RoughStart(truth), SyntheticSky.Sky(), Size, Size);

        Assert.NotNull(fit);
        Assert.InRange(fit!.StarsMatched, 60, 1000);
        Assert.InRange(fit.RmsErrorPx, 0, 1.0);
        Assert.InRange(fit.RmsErrorDeg, 0, 0.1);
        Assert.InRange(fit.Lens.CentreX, truth.CentreX - 1, truth.CentreX + 1);
        Assert.InRange(fit.Lens.CentreY, truth.CentreY - 1, truth.CentreY + 1);
        Assert.InRange(SkyGeometry.SeparationDeg(new(0, fit.Lens.NorthRotationDeg), new(0, truth.NorthRotationDeg)), 0, 0.1);
        Assert.Equal(fit.StarsMatched, fit.Lens.StarsMatched);
        Assert.False(fit.Lens.Manual);
    }

    [Fact]
    public void FittedModelPutsStarsWhereTheTruthDoes()
    {
        LensCalibration truth = SyntheticSky.MarksLens();
        LensFitResult fit = new LensFitter().Fit(SyntheticSky.Detected(truth, Size), RoughStart(truth), SyntheticSky.Sky(), Size, Size)!;
        var a = new LensProjection(truth);
        var b = new LensProjection(fit.Lens);
        foreach (HorizontalCoordinate h in new HorizontalCoordinate[] { new(90, 0), new(60, 45), new(30, 200), new(15, 300) })
        {
            PixelPoint pa = a.ToPixel(h), pb = b.ToPixel(h);
            Assert.InRange(Math.Sqrt(((pa.X - pb.X) * (pa.X - pb.X)) + ((pa.Y - pb.Y) * (pa.Y - pb.Y))), 0, 2.0);
        }
    }

    [Fact]
    public void TooFewStarsGivesNull()
    {
        LensCalibration truth = SyntheticSky.MarksLens();
        List<DetectedStar> few = SyntheticSky.Detected(truth, Size, detectedMag: 0.3, junk: 0);
        Assert.Null(new LensFitter().Fit(few, RoughStart(truth), SyntheticSky.Sky(), Size, Size));
    }
}
