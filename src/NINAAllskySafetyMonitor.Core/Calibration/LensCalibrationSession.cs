using System.Globalization;
using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>
/// Calibrates the lens from one clear frame (UC-12): detects stars, finds a starting model automatically
/// (or from stars the imager identifies), fits it to the catalogue and checks the result. Star detection
/// is kept for the last frame, so identifying stars after a failed search does not detect again.
/// </summary>
[Requirement("REQ-19", "REQ-20")]
public sealed class LensCalibrationSession
{
    private readonly StarCatalogue catalogue;
    private readonly StarDetector detector;
    private readonly InitialGuessSearch search;
    private readonly LensFitter fitter;
    private readonly CalibrationQualityCheck quality;
    private readonly Func<LensFitSettings> settings;
    private CalibrationFrame? lastFrame;
    private IReadOnlyList<DetectedStar> lastDetected = Array.Empty<DetectedStar>();

    public LensCalibrationSession(StarCatalogue catalogue, StarDetector detector, InitialGuessSearch search, LensFitter fitter, CalibrationQualityCheck quality, Func<LensFitSettings> settings)
    {
        this.catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        this.detector = detector ?? throw new ArgumentNullException(nameof(detector));
        this.search = search ?? throw new ArgumentNullException(nameof(search));
        this.fitter = fitter ?? throw new ArgumentNullException(nameof(fitter));
        this.quality = quality ?? throw new ArgumentNullException(nameof(quality));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>A session with the embedded catalogue and default parts.</summary>
    public static LensCalibrationSession Create(Func<LensFitSettings> settings) =>
        new(StarCatalogue.LoadEmbedded(), new StarDetector(), new InitialGuessSearch(), new LensFitter(), new CalibrationQualityCheck(settings), settings);

    /// <summary>Catalogue stars on the sky at the frame's time, brightest first.</summary>
    public IReadOnlyList<SkyStar> Sky(CalibrationFrame frame, SiteLocation site) =>
        SkyStars.AboveHorizon(catalogue.BrighterThan(settings().CatalogueLimitMagnitude), site, frame.CaptureUtc);

    /// <summary>Automatic calibration.</summary>
    public CalibrationOutcome Calibrate(CalibrationFrame frame, SiteLocation site)
    {
        ArgumentNullException.ThrowIfNull(frame);
        IReadOnlyList<DetectedStar> detected = Detect(frame);
        IReadOnlyList<SkyStar> sky = Sky(frame, site);
        var start = search.Search(detected, frame.Frame.Width, frame.Frame.Height, sky);
        if (start is null)
        {
            return new CalibrationOutcome(null, Array.Empty<string>(), true, detected.Count,
                $"No star pattern found automatically among {detected.Count} spots: identify two or three bright stars.");
        }

        return FitFrom(start, detected, sky, frame);
    }

    /// <summary>Calibration started from stars the imager identified (two or more).</summary>
    public CalibrationOutcome CalibrateFromIdentified(CalibrationFrame frame, SiteLocation site, IReadOnlyList<StarIdentification> picks)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(picks);
        IReadOnlyList<DetectedStar> detected = Detect(frame);
        IReadOnlyList<SkyStar> sky = Sky(frame, site);
        var placed = picks
            .Select(p => (Star: SkyStars.AboveHorizon(new[] { p.Star }, site, frame.CaptureUtc, -90).Single(), p.Position))
            .ToList();
        var start = search.FromIdentified(placed, detected, frame.Frame.Width, frame.Frame.Height, sky);
        if (start is null)
        {
            return new CalibrationOutcome(null, Array.Empty<string>(), true, detected.Count, "Identify at least two different stars.");
        }

        return FitFrom(start, detected, sky, frame);
    }

    private CalibrationOutcome FitFrom(Geometry.LensCalibration start, IReadOnlyList<DetectedStar> detected, IReadOnlyList<SkyStar> sky, CalibrationFrame frame)
    {
        LensFitResult? result = fitter.Fit(detected, start, sky, frame.Frame.Width, frame.Frame.Height);
        if (result is null)
        {
            return new CalibrationOutcome(null, Array.Empty<string>(), true, detected.Count,
                "Too few stars matched to fit the lens: identify two or three bright stars, or try a clearer frame.");
        }

        result = result with { Lens = result.Lens with { CalibratedUtc = frame.CaptureUtc, ImageWidth = frame.Frame.Width, ImageHeight = frame.Frame.Height } };
        IReadOnlyList<string> warnings = quality.Warnings(result);
        string message = string.Create(CultureInfo.InvariantCulture,
            $"{result.StarsMatched} stars matched, error {result.RmsErrorPx:0.0} px ({result.RmsErrorDeg:0.00}°).");
        return new CalibrationOutcome(result, warnings, false, detected.Count, message);
    }

    private IReadOnlyList<DetectedStar> Detect(CalibrationFrame frame)
    {
        if (!ReferenceEquals(frame, lastFrame))
        {
            lastDetected = detector.Detect(frame.Frame);
            lastFrame = frame;
        }

        return lastDetected;
    }
}
