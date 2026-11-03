using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// Judges one saved frame with the plugin's Core code: operating window (Sun below the limit), sky-frame
/// check, then CloudDetector for every grid target inside the working area. The harness keeps its own
/// clear-sky reference: until one is captured the thresholds are treated as tuned, so the first clear
/// moonless frame becomes the reference (or the saved one is used when asked). Weather is not replayed.
/// </summary>
[Requirement("REQ-42")]
public sealed class FrameAnalyser
{
    private readonly MonitorSettings settings;
    private readonly IFrameDecoder decoder;
    private readonly TargetGrid grid;
    private readonly IAstroCalculator astro = new CoreAstroCalculator();
    private readonly SkyFrameCheck skyFrameCheck = new();
    private readonly CloudDetectionSettings cloudSettings;
    private readonly CloudDetector detector;
    private readonly LensCalibration lens;
    private readonly WorkingArea area;
    private readonly SiteLocation site;

    public FrameAnalyser(MonitorSettings settings, ReplayOptions options, IFrameDecoder decoder, TargetGrid grid)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(options);
        this.decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        lens = settings.Calibration.Lens is { RadialCoefficients: [> 0, ..] } l
            ? l
            : throw new InvalidOperationException("The settings file has no lens calibration: calibrate the lens in the plugin first.");
        area = new WorkingArea(settings.Calibration.WorkingArea, new LensProjection(lens));
        site = new SiteLocation(settings.Site.LatitudeDeg, settings.Site.LongitudeDeg, settings.Site.ElevationM);
        Reference = options.UseSettingsReference ? settings.Calibration.ClearSkyReference : null;

        // A copy, so the reference capture can be allowed without changing the thresholds.
        cloudSettings = SettingsStore.Clone(settings).Cloud;
        detector = new CloudDetector(() => cloudSettings, StarCatalogue.LoadEmbedded());
    }

    /// <summary>The clear-sky reference in use, once there is one.</summary>
    public ClearSkyReference? Reference { get; private set; }

    public LensCalibration Lens => lens;

    public WorkingAreaDefinition WorkingArea => settings.Calibration.WorkingArea;

    /// <summary>True when the Sun was below the operating-window limit, so the frame will be judged.</summary>
    public bool IsNight(SavedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return astro.SunAltitudeDeg(site, frame.CaptureUtc) < settings.Window.SunAltitudeLimitDeg;
    }

    public FrameResult Analyse(SavedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        double sunAlt = astro.SunAltitudeDeg(site, frame.CaptureUtc);
        if (sunAlt >= settings.Window.SunAltitudeLimitDeg)
        {
            return new FrameResult(frame, sunAlt, false, null, Array.Empty<TargetResult>(), false);
        }

        GrayFrame gray = decoder.Decode(File.ReadAllBytes(frame.Path));
        ConditionCheck sky = skyFrameCheck.Evaluate(gray, area, lens);
        if (!sky.Passed) return new FrameResult(frame, sunAlt, true, sky, Array.Empty<TargetResult>(), false);

        var moon = new MoonState(astro.MoonPosition(site, frame.CaptureUtc), astro.MoonIllumination(frame.CaptureUtc));
        cloudSettings.Tuned = Reference is null;
        bool captured = false;
        var targets = new List<TargetResult>();
        foreach ((ReplayTarget target, RegionOfInterest roi) in grid.RoisAt(frame.CaptureUtc, site, settings, area))
        {
            CloudDetection d = detector.Assess(gray, roi, settings.Calibration.WorkingArea, lens, site, frame.CaptureUtc, moon, Reference);
            if (d.CapturedReference is ClearSkyReference r && Reference is null)
            {
                Reference = r;
                captured = true;
            }

            targets.Add(new TargetResult(target, roi, d.Assessment));
        }

        return new FrameResult(frame, sunAlt, true, sky, targets, captured);
    }
}
