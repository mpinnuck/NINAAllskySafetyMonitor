using System.Globalization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Judges cloud in the ROI of a sky frame (UC-08). Cloud = the star test says cloud OR the brightness test
/// says cloud. With too few expected stars the brightness test decides alone; with no clear-sky reference
/// the star test decides alone; with neither, cloud is assumed (fail-safe). While no reference exists,
/// the thresholds are tuned and the Moon is down, the star test is also run over the whole working area,
/// and the first frame that is clear there becomes the reference.
/// </summary>
[Requirement("REQ-28", "REQ-29", "REQ-30", "REQ-31")]
public sealed class CloudDetector
{
    /// <summary>Margin around the ROI searched for stars, in pixels.</summary>
    private const int SearchMargin = 16;

    private readonly Func<CloudDetectionSettings> settings;
    private readonly RoiMaskBuilder masks;
    private readonly ExpectedStarPredictor predictor;
    private readonly StarDetector detector;
    private readonly StarMatcher matcher;
    private readonly StarFractionTest starTest;
    private readonly BackgroundStatistics background;
    private readonly BrightnessTest brightnessTest;
    private readonly MoonBrightnessAdjuster moonAdjuster;
    private readonly ClearSkyReferenceBuilder referenceBuilder;

    public CloudDetector(Func<CloudDetectionSettings> settings, StarCatalogue catalogue)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(catalogue);
        masks = new RoiMaskBuilder();
        predictor = new ExpectedStarPredictor(catalogue);
        detector = new StarDetector();
        matcher = new StarMatcher();
        starTest = new StarFractionTest(settings);
        background = new BackgroundStatistics();
        brightnessTest = new BrightnessTest(settings);
        moonAdjuster = new MoonBrightnessAdjuster();
        referenceBuilder = new ClearSkyReferenceBuilder();
    }

    /// <summary>How far, in pixels, a detected star may be from where a catalogue star should be.</summary>
    public static double MatchTolerancePx(LensCalibration lens, int imageWidth)
    {
        ArgumentNullException.ThrowIfNull(lens);
        if (lens.Manual || lens.RmsErrorPx <= 0) return Math.Max(8.0, imageWidth / 250.0);
        return Math.Max(4.0, 3.0 * lens.RmsErrorPx);
    }

    public CloudDetection Assess(
        GrayFrame frame,
        RegionOfInterest roi,
        WorkingAreaDefinition workingArea,
        LensCalibration lens,
        SiteLocation site,
        DateTime utc,
        MoonState? moon,
        ClearSkyReference? reference)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(roi);
        ArgumentNullException.ThrowIfNull(workingArea);
        ArgumentNullException.ThrowIfNull(lens);
        ArgumentNullException.ThrowIfNull(site);

        CloudDetectionSettings s = settings();
        var projection = new LensProjection(lens);
        var area = new WorkingArea(workingArea, projection);
        PixelMask areaMask = masks.BuildArea(area, projection, frame.Width, frame.Height);
        PixelMask roiMask = masks.Build(roi, area, projection, frame.Width, frame.Height);
        double tolerance = MatchTolerancePx(lens, frame.Width);

        // Star test in the ROI.
        (StarTestResult stars, IReadOnlyList<ExpectedStar> expected, IReadOnlyList<DetectedStar?> found) =
            StarTest(frame, roiMask, projection, site, utc, moon, tolerance, s);

        // Brightness test against the reference.
        BlockStatistics blocks = background.Measure(frame, areaMask);
        double moonAlt = moon?.Position.AltDeg ?? -90;
        double moonFactor = moonAdjuster.Factor(moonAlt, moon?.Illumination ?? 0);
        BrightnessTestResult brightness = brightnessTest.Evaluate(blocks, reference, roiMask, moonFactor);

        // First tuned clear night: capture the reference (not worth the whole-area search when the ROI is cloudy).
        ClearSkyReference? captured = null;
        if (reference is null && !stars.Cloud && referenceBuilder.CanCapture(s.Tuned, moonAlt))
        {
            StarTestResult whole = roi.UsesWholeWorkingArea
                ? stars
                : StarTest(frame, areaMask, projection, site, utc, moon, tolerance, s).Result;
            captured = referenceBuilder.TryCapture(blocks, whole, moonAlt, s.Tuned, utc);
        }

        bool starUsable = !stars.Inconclusive;
        bool brightnessUsable = !brightness.Skipped;
        bool cloud = (starUsable && stars.Cloud) || (brightnessUsable && brightness.Cloud) || (!starUsable && !brightnessUsable);

        var assessment = new CloudAssessment(
            stars.Expected,
            stars.Matched,
            stars.Fraction,
            brightness.RelativeBrightness,
            brightness.UniformityRatio,
            stars.Inconclusive,
            brightness.Skipped,
            cloud,
            Summary(stars, brightness, cloud, s, moonFactor))
        {
            Expected = expected.Select(e => e.Position).ToList(),
            Found = found.Select(f => f is not null).ToList(),
            Moon = moon,
        };
        return new CloudDetection(assessment, captured);
    }

    private (StarTestResult Result, IReadOnlyList<ExpectedStar> Expected, IReadOnlyList<DetectedStar?> Found) StarTest(
        GrayFrame frame, PixelMask mask, LensProjection projection, SiteLocation site, DateTime utc, MoonState? moon, double tolerance, CloudDetectionSettings s)
    {
        IReadOnlyList<ExpectedStar> expected = predictor.Predict(mask, projection, site, utc, s.LimitingMagnitude, moon, s.MoonExclusionDeg);
        if (expected.Count == 0 || mask.Bounds is not PixelRect bounds)
        {
            return (starTest.Evaluate(0, 0), expected, Array.Empty<DetectedStar?>());
        }

        IReadOnlyList<DetectedStar> detected = detector.Detect(frame, mask, bounds.Inflate(SearchMargin, frame.Width, frame.Height));
        IReadOnlyList<DetectedStar?> found = matcher.Match(expected, detected, tolerance);
        return (starTest.Evaluate(expected.Count, found.Count(f => f is not null)), expected, found);
    }

    private static string Summary(StarTestResult stars, BrightnessTestResult brightness, bool cloud, CloudDetectionSettings s, double moonFactor)
    {
        string starText = string.Create(CultureInfo.InvariantCulture, $"{stars.Matched} of {stars.Expected} stars");
        if (stars.Fraction is double f) starText += string.Create(CultureInfo.InvariantCulture, $" ({f:0%})");

        if (stars.Inconclusive && brightness.Skipped)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Cloud unknown: only {stars.Expected} stars expected in the ROI (need {s.MinExpectedStars}) and no clear-sky reference yet");
        }

        if (!cloud)
        {
            return brightness.Skipped
                ? $"Clear: {starText}"
                : string.Create(CultureInfo.InvariantCulture, $"Clear: {(stars.Inconclusive ? $"only {stars.Expected} stars expected" : starText)}, brightness {brightness.RelativeBrightness:0.00}×");
        }

        if (!stars.Inconclusive && stars.Cloud) return $"Cloud: {starText}";

        return brightness.RelativeBrightness > s.MaxBrightnessRatio * Math.Max(1.0, moonFactor)
            ? string.Create(CultureInfo.InvariantCulture, $"Cloud: ROI {brightness.RelativeBrightness:0.00}× brighter than clear sky")
            : string.Create(CultureInfo.InvariantCulture, $"Cloud: ROI {brightness.UniformityRatio:0.00}× less uniform than clear sky");
    }
}
