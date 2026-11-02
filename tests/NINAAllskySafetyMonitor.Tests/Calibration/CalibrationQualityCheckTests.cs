using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Calibration;

[Requirement("REQ-20")]
public sealed class CalibrationQualityCheckTests
{
    private readonly LensFitSettings settings = new();

    private static LensFitResult Result(int stars, double rmsDeg)
    {
        var star = new SkyStar(new CatalogueStar(1, 0, 0, 1), new HorizontalCoordinate(45, 0));
        var matches = Enumerable.Range(0, stars).Select(_ => new StarMatch(star, new PixelPoint(0, 0), new PixelPoint(0, 0), 1, rmsDeg)).ToList();
        return new LensFitResult(new LensCalibration(), matches, 1, rmsDeg);
    }

    [Fact]
    public void GoodFitHasNoWarnings() => Assert.Empty(new CalibrationQualityCheck(() => settings).Warnings(Result(33, 0.15)));

    [Fact]
    public void TooFewStarsIsWarned() =>
        Assert.Contains("Only 10 stars matched (at least 15 expected)", Assert.Single(new CalibrationQualityCheck(() => settings).Warnings(Result(10, 0.1))));

    [Fact]
    public void LargeErrorIsWarned() =>
        Assert.Contains("Fit error 0.35° is above the 0.20° limit", Assert.Single(new CalibrationQualityCheck(() => settings).Warnings(Result(40, 0.35))));

    [Fact]
    public void LimitsComeFromTheSettings()
    {
        settings.MinMatchedStars = 50;
        settings.MaxFitErrorDeg = 0.1;
        Assert.Equal(2, new CalibrationQualityCheck(() => settings).Warnings(Result(33, 0.15)).Count);
    }
}
