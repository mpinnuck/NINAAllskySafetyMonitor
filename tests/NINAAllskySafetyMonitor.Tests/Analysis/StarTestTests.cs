using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Analysis;

/// <summary>ExpectedStarPredictor, StarMatcher and StarFractionTest.</summary>
[Requirement("REQ-29", "REQ-31")]
public sealed class StarTestTests
{
    private static readonly LensProjection Projection = new(SyntheticCloudSky.Lens);

    private static PixelMask RoiMask() =>
        new RoiMaskBuilder().Build(SyntheticCloudSky.Roi(SyntheticCloudSky.SouthHigh), new WorkingArea(new WorkingAreaDefinition(), Projection), Projection, 1000, 1000);

    private static IReadOnlyList<ExpectedStar> Predict(double mag = 4.4, MoonState? moon = null) =>
        new ExpectedStarPredictor(StarCatalogue.LoadEmbedded()).Predict(RoiMask(), Projection, SyntheticSky.Site, SyntheticSky.FrameUtc, mag, moon, 15);

    [Fact]
    public void ExpectedStarsAreInsideTheRoiBrightestFirst()
    {
        IReadOnlyList<ExpectedStar> stars = Predict();
        Assert.NotEmpty(stars);
        Assert.All(stars, s => Assert.InRange(SkyGeometry.SeparationDeg(s.Sky, SyntheticCloudSky.SouthHigh), 0, 15.5));
        Assert.All(stars, s => Assert.True(s.Star.Magnitude <= 4.4));
        Assert.Equal(stars.OrderBy(s => s.Star.Magnitude).Select(s => s.Star.Id), stars.Select(s => s.Star.Id));
    }

    [Fact]
    public void FainterLimitExpectsMoreStars() => Assert.True(Predict(4.4).Count > Predict(4.0).Count);

    [Fact]
    public void MoonBelowTheHorizonExcludesNothing() =>
        Assert.Equal(Predict().Count, Predict(moon: new MoonState(new HorizontalCoordinate(-5, 180), 1)).Count);

    [Fact]
    public void StarsWithinTheExclusionOfTheMoonAreDropped()
    {
        var moon = new MoonState(new HorizontalCoordinate(65, 180), 1);
        Assert.All(Predict(moon: moon), s => Assert.True(SkyGeometry.SeparationDeg(s.Sky, moon.Position) >= 15));
    }

    [Fact]
    public void MatcherPairsNearestUnclaimedWithinTolerance()
    {
        var star = new CatalogueStar(1, 0, 0, 1);
        var expected = new[] { new ExpectedStar(star, default, new PixelPoint(100, 100)), new ExpectedStar(star, default, new PixelPoint(103, 100)), new ExpectedStar(star, default, new PixelPoint(300, 300)) };
        var detected = new[] { new DetectedStar(new PixelPoint(101, 100), 10), new DetectedStar(new PixelPoint(310, 300), 10) };

        IReadOnlyList<DetectedStar?> found = new StarMatcher().Match(expected, detected, 4);

        Assert.Same(detected[0], found[0]);
        Assert.Null(found[1]); // the only near star is already claimed
        Assert.Null(found[2]); // 10 px away
    }

    [Theory]
    [InlineData(11, 9, false, false)]
    [InlineData(11, 7, false, true)]
    [InlineData(7, 5, false, false)]
    [InlineData(7, 4, false, true)]
    [InlineData(3, 2, true, false)]
    [InlineData(2, 0, true, false)]
    [InlineData(0, 0, true, false)]
    public void FractionTest(int expected, int matched, bool inconclusive, bool cloud)
    {
        StarTestResult r = new StarFractionTest(() => new CloudDetectionSettings()).Evaluate(expected, matched);
        Assert.Equal(inconclusive, r.Inconclusive);
        Assert.Equal(cloud, r.Cloud);
        Assert.Equal(expected == 0 ? null : (double)matched / expected, r.Fraction);
    }

    [Fact]
    public void LowCountFractionClearTakesPrecedenceOverProbability()
    {
        var settings = new CloudDetectionSettings { MinExpectedStars = 8, MinStarFraction = 0.1 };

        StarTestResult result = new StarFractionTest(() => settings).Evaluate(5, 1);

        Assert.False(result.Inconclusive);
        Assert.False(result.Cloud);
    }

    [Fact]
    public void ZeroMinimumFractionMakesEveryCountAboveFloorClear()
    {
        var settings = new CloudDetectionSettings { MinExpectedStars = 8, MinStarFraction = 0 };

        StarTestResult result = new StarFractionTest(() => settings).Evaluate(5, 0);

        Assert.False(result.Inconclusive);
        Assert.False(result.Cloud);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, -1)]
    [InlineData(2, 3)]
    public void InvalidStarCountsAreRejected(int expected, int matched) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StarFractionTest(() => new CloudDetectionSettings()).Evaluate(expected, matched));
}
