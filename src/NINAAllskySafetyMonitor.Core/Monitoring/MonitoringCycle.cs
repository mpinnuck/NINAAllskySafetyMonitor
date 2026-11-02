using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>
/// One monitoring pass (UC-04). Outside the operating window the status is Idle and nothing is
/// fetched. Inside it, the image is fetched (at most once per image interval, so a cycle triggered early
/// by a mount change reuses the latest image) and checked for freshness, the weather is polled (at its
/// own interval) and checked, and the ROI is placed from the mount's RA/Dec within the working area.
/// The image is then decoded (once per image), checked to be a sky frame, and judged for cloud in the
/// ROI (SD-03). The reason lists every failed condition; the decision engine turns an all-passed cycle
/// into Safe. Settings are read at the start of every cycle.
/// </summary>
[Requirement("REQ-05", "REQ-10", "REQ-14", "REQ-28", "REQ-37", "REQ-50", "REQ-60")]
public sealed class MonitoringCycle : IMonitoringCycle
{
    private readonly Func<MonitorSettings> settings;
    private readonly IClock clock;
    private readonly SiteLocationResolver siteResolver;
    private readonly OperatingWindow window;
    private readonly ImageAcquirer acquirer;
    private readonly ImageFreshnessEvaluator freshness;
    private readonly WeatherPoller weather;
    private readonly WeatherEvaluator weatherEvaluator;
    private readonly IMountStateSource? mount;
    private readonly RoiCalculator roiCalculator;
    private readonly RoiBuilder roiBuilder;
    private readonly IFrameDecoder? decoder;
    private readonly CloudDetector? cloudDetector;
    private readonly IAstroCalculator? astro;
    private readonly Action<ClearSkyReference>? referenceCaptured;
    private readonly SkyFrameCheck skyFrameCheck = new();
    private DateTime? lastImageFetchUtc;
    private SkyImage? decodedImage;
    private GrayFrame? decodedFrame;
    private ClearSkyReference? sessionReference;

    public MonitoringCycle(
        Func<MonitorSettings> settings,
        IClock clock,
        SiteLocationResolver siteResolver,
        OperatingWindow window,
        ImageAcquirer acquirer,
        ImageFreshnessEvaluator freshness,
        WeatherPoller weather,
        WeatherEvaluator weatherEvaluator,
        IMountStateSource? mount,
        RoiCalculator roiCalculator,
        RoiBuilder roiBuilder,
        IFrameDecoder? decoder = null,
        CloudDetector? cloudDetector = null,
        IAstroCalculator? astro = null,
        Action<ClearSkyReference>? referenceCaptured = null)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.siteResolver = siteResolver ?? throw new ArgumentNullException(nameof(siteResolver));
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.acquirer = acquirer ?? throw new ArgumentNullException(nameof(acquirer));
        this.freshness = freshness ?? throw new ArgumentNullException(nameof(freshness));
        this.weather = weather ?? throw new ArgumentNullException(nameof(weather));
        this.weatherEvaluator = weatherEvaluator ?? throw new ArgumentNullException(nameof(weatherEvaluator));
        this.mount = mount;
        this.roiCalculator = roiCalculator ?? throw new ArgumentNullException(nameof(roiCalculator));
        this.roiBuilder = roiBuilder ?? throw new ArgumentNullException(nameof(roiBuilder));
        this.decoder = decoder;
        this.cloudDetector = cloudDetector;
        this.astro = astro;
        this.referenceCaptured = referenceCaptured;
    }

    /// <inheritdoc />
    public async Task<MonitorSnapshot> RunOnceAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        MonitorSettings s = settings();
        bool untuned = !s.Cloud.Tuned;
        MountState? mountState = mount?.GetCurrent();

        SiteLocation site = siteResolver.Resolve();
        ConditionCheck windowCheck = window.Evaluate(site, clock.UtcNow, s.Window.SunAltitudeLimitDeg);

        if (!windowCheck.Passed)
        {
            var idle = new SafetyStatus(SafetyState.Idle, false, windowCheck.Reason, null, OverrideMode.Auto, untuned, clock.UtcNow);
            return new MonitorSnapshot(null, null, null, null, new SafetyConditions(new[] { windowCheck }), idle, mountState);
        }

        // Image and weather are independent, so fetch them together.
        await Task.WhenAll(AcquireImageIfDueAsync(s, ct), weather.PollIfDueAsync(ct)).ConfigureAwait(false);

        DateTime now = clock.UtcNow;
        RegionOfInterest roi = PlaceRoi(s, mountState, site, now);

        var checks = new List<ConditionCheck>
        {
            windowCheck,
            freshness.Evaluate(acquirer.LatestImage, acquirer.LastSuccessUtc, acquirer.LastError, now, s.ImageSource),
        };
        checks.AddRange(weatherEvaluator.Evaluate(weather.Latest, weather.LastError, now, s.Weather));

        // Decoding and analysis take a second or two: keep them off the caller's thread.
        SkyImage? image = acquirer.LatestImage;
        (ConditionCheck cloudCheck, CloudAssessment? cloud, bool notSky) = await Task.Run(() => AssessCloud(s, image, roi, site, now), ct).ConfigureAwait(false);
        checks.Add(cloudCheck);

        // Distinct: with no weather data, rain, wind and data age all fail for the same reason.
        string reason = string.Join(" · ", checks.Where(c => !c.Passed).Select(c => c.Reason).Distinct());
        var status = new SafetyStatus(SafetyState.Unsafe, false, reason, null, OverrideMode.Auto, untuned, now);
        WeatherObservation? obs = s.Weather.Enabled ? weather.Latest : null;
        return new MonitorSnapshot(image, roi, cloud, obs, new SafetyConditions(checks), status, mountState) { NotASkyFrame = notSky };
    }

    /// <summary>Decodes the image, checks it is a sky frame and judges cloud in the ROI.</summary>
    private (ConditionCheck Check, CloudAssessment? Cloud, bool NotSkyFrame) AssessCloud(MonitorSettings s, SkyImage? image, RegionOfInterest roi, SiteLocation site, DateTime now)
    {
        if (decoder is null || cloudDetector is null) return (Unknown("Cloud detection unavailable"), null, false);
        if (image is null) return (Unknown("No image to judge cloud"), null, false);
        if (s.Calibration.Lens is not { RadialCoefficients: [> 0, ..] } lens)
        {
            return (Unknown("Lens not calibrated: calibrate it in the plugin options"), null, false);
        }

        GrayFrame frame;
        try
        {
            frame = Decode(image);
        }
        catch (Exception e)
        {
            return (Unknown($"Image could not be decoded: {e.Message}"), null, true);
        }

        var area = new WorkingArea(s.Calibration.WorkingArea, new LensProjection(lens));
        ConditionCheck skyFrame = skyFrameCheck.Evaluate(frame, area, lens);
        if (!skyFrame.Passed) return (skyFrame, null, true);

        MoonState? moon = astro is null ? null : new MoonState(astro.MoonPosition(site, now), astro.MoonIllumination(now));
        ClearSkyReference? reference = s.Calibration.ClearSkyReference ?? sessionReference;
        CloudDetection result = cloudDetector.Assess(frame, roi, s.Calibration.WorkingArea, lens, site, now, moon, reference);
        if (result.CapturedReference is ClearSkyReference captured)
        {
            sessionReference = captured;
            referenceCaptured?.Invoke(captured);
        }

        CloudAssessment cloud = result.Assessment;
        ConditionCheck check = cloud.Undetermined
            ? Unknown(cloud.Summary)
            : new ConditionCheck(SafetyCondition.NoCloud, !cloud.CloudPresent, cloud.Summary);
        return (check, cloud, false);
    }

    /// <summary>Decodes the image, reusing the last result when a cycle sees the same image again.</summary>
    private GrayFrame Decode(SkyImage image)
    {
        if (ReferenceEquals(image, decodedImage) && decodedFrame is not null) return decodedFrame;
        GrayFrame frame = decoder!.Decode(image.JpegBytes);
        decodedImage = image;
        decodedFrame = frame;
        return frame;
    }

    /// <summary>Cloud can't be judged: undetermined, not detected (REQ-50).</summary>
    private static ConditionCheck Unknown(string reason) => ConditionCheck.Unknown(SafetyCondition.NoCloud, reason);

    /// <summary>Fetches a new image once the image interval has passed (1 s tolerance for loop timing).</summary>
    private Task AcquireImageIfDueAsync(MonitorSettings s, CancellationToken ct)
    {
        DateTime now = clock.UtcNow;
        if (lastImageFetchUtc is DateTime last && now - last < TimeSpan.FromSeconds(s.ImageSource.IntervalS - 1))
        {
            return Task.CompletedTask;
        }

        lastImageFetchUtc = now;
        return acquirer.AcquireAsync(ct);
    }

    private RegionOfInterest PlaceRoi(MonitorSettings s, MountState? mountState, SiteLocation site, DateTime now)
    {
        (HorizontalCoordinate? centre, string? unusable) = roiCalculator.CalculateCentre(mountState, site, now);
        LensProjection? projection = s.Calibration.Lens is { RadialCoefficients: [> 0, ..] } lens ? new LensProjection(lens) : null;
        var area = new WorkingArea(s.Calibration.WorkingArea, projection);
        return roiBuilder.Build(centre, unusable, s.Roi.RadiusDeg, s.Roi.MinInsideFraction, area);
    }
}
