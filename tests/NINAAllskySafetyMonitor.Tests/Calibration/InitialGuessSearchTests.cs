using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Calibration;

[Requirement("REQ-20")]
public sealed class InitialGuessSearchTests
{
    private const int Size = 2500;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FindsTheZenithRotationAndEastDirection(bool eastClockwise)
    {
        LensCalibration truth = SyntheticSky.MarksLens(eastClockwise);
        List<DetectedStar> spots = SyntheticSky.Detected(truth, Size);

        LensCalibration? guess = new InitialGuessSearch().Search(spots, Size, Size, SyntheticSky.Sky());

        Assert.NotNull(guess);
        Assert.Equal(eastClockwise, guess!.EastIsClockwise);
        Assert.InRange(guess.CentreX, truth.CentreX - 40, truth.CentreX + 40);
        Assert.InRange(guess.CentreY, truth.CentreY - 40, truth.CentreY + 40);
        Assert.InRange(SkyGeometry.SeparationDeg(new(0, guess.NorthRotationDeg), new(0, truth.NorthRotationDeg)), 0, 3);
    }

    [Fact]
    public void NothingConvincingGivesNull()
    {
        var rng = new Random(9);
        var junk = Enumerable.Range(0, 200)
            .Select(_ => new DetectedStar(new PixelPoint(rng.NextDouble() * Size, rng.NextDouble() * Size), 10, 10 + rng.NextDouble(), 4))
            .OrderByDescending(d => d.Peak).ToList();
        Assert.Null(new InitialGuessSearch().Search(junk, Size, Size, SyntheticSky.Sky()));
    }

    [Fact]
    public void ThreeIdentifiedStarsGiveAStartingModel()
    {
        LensCalibration truth = SyntheticSky.MarksLens();
        var projection = new LensProjection(truth);
        IReadOnlyList<SkyStar> sky = SyntheticSky.Sky();
        var picks = new[] { "Canopus", "Achernar", "Rigel" }
            .Select(n => sky.Single(s => s.Star.Name == n))
            .Select(s => (s, projection.ToPixel(s.Position)))
            .ToList();

        LensCalibration? start = new InitialGuessSearch().FromIdentified(picks, SyntheticSky.Detected(truth, Size), Size, Size, sky);

        Assert.NotNull(start);
        Assert.False(start!.EastIsClockwise);
        Assert.InRange(start.CentreX, truth.CentreX - 60, truth.CentreX + 60);
        Assert.InRange(start.CentreY, truth.CentreY - 60, truth.CentreY + 60);
    }

    [Fact]
    public void OneIdentifiedStarIsNotEnough()
    {
        IReadOnlyList<SkyStar> sky = SyntheticSky.Sky();
        var picks = new[] { (sky[0], new PixelPoint(100, 100)) };
        Assert.Null(new InitialGuessSearch().FromIdentified(picks, Array.Empty<DetectedStar>(), Size, Size, sky));
    }
}
