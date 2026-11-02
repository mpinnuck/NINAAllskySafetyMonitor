using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Calibration;

/// <summary>End to end on a synthetic 1000 × 1000 frame with hot pixels: detect, search, fit, check.</summary>
[Requirement("REQ-19", "REQ-20")]
public sealed class LensCalibrationSessionTests
{
    private const int Size = 1000;
    private static readonly LensCalibration Truth = SyntheticSky.Scaled(SyntheticSky.MarksLens(), Size / 2500.0);
    private readonly LensFitSettings settings = new();

    private static CalibrationFrame Frame() =>
        new(SyntheticSky.Frame(Truth, Size).Frame, SyntheticSky.FrameUtc, "allsky-20261020022133.jpg");

    [Fact]
    public void CalibratesAutomaticallyDespiteHotPixels()
    {
        CalibrationOutcome outcome = LensCalibrationSession.Create(() => settings).Calibrate(Frame(), SyntheticSky.Site);

        Assert.False(outcome.NeedsIdentification);
        Assert.NotNull(outcome.Result);
        Assert.Empty(outcome.Warnings);
        LensCalibration lens = outcome.Result!.Lens;
        Assert.InRange(outcome.Result.StarsMatched, 30, 1000);
        Assert.InRange(outcome.Result.RmsErrorPx, 0, 1.0);
        Assert.InRange(lens.CentreX, Truth.CentreX - 1.5, Truth.CentreX + 1.5);
        Assert.InRange(lens.CentreY, Truth.CentreY - 1.5, Truth.CentreY + 1.5);
        Assert.Equal(SyntheticSky.FrameUtc, lens.CalibratedUtc);
        Assert.Equal((Size, Size), (lens.ImageWidth, lens.ImageHeight));
        Assert.StartsWith($"{outcome.Result.StarsMatched} stars matched, error ", outcome.Message);
    }

    [Fact]
    public void IdentifiedStarsGiveTheSameResult()
    {
        LensCalibrationSession session = LensCalibrationSession.Create(() => settings);
        CalibrationFrame frame = Frame();
        var projection = new LensProjection(Truth);
        IReadOnlyList<SkyStar> sky = session.Sky(frame, SyntheticSky.Site);
        var picks = new[] { "Canopus", "Achernar" }
            .Select(n => sky.Single(s => s.Star.Name == n))
            .Select(s => new StarIdentification(s.Star, projection.ToPixel(s.Position)))
            .ToList();

        CalibrationOutcome outcome = session.CalibrateFromIdentified(frame, SyntheticSky.Site, picks);

        Assert.NotNull(outcome.Result);
        Assert.InRange(outcome.Result!.Lens.CentreX, Truth.CentreX - 1.5, Truth.CentreX + 1.5);
        Assert.InRange(outcome.Result.Lens.CentreY, Truth.CentreY - 1.5, Truth.CentreY + 1.5);
    }

    [Fact]
    public void BlankFrameAsksForIdentification()
    {
        var blank = new CalibrationFrame(new GrayFrame(400, 400, Enumerable.Repeat(20f, 400 * 400).ToArray()), SyntheticSky.FrameUtc, "blank");
        CalibrationOutcome outcome = LensCalibrationSession.Create(() => settings).Calibrate(blank, SyntheticSky.Site);
        Assert.True(outcome.NeedsIdentification);
        Assert.Null(outcome.Result);
        Assert.Contains("identify two or three bright stars", outcome.Message);
    }
}
