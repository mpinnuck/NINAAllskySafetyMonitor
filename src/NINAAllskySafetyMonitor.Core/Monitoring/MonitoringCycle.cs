using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>
/// One monitoring pass (UC-04). Outside the operating window the status is Idle and nothing is
/// fetched. Inside it, the image is fetched and checked for freshness and the weather is polled (at its
/// own interval) and checked. The reason lists every failed condition. Until cloud detection is added
/// (slice 6) the status stays Unsafe even when everything else passes. Settings are read at the start
/// of every cycle, so saved changes take effect without reconnecting.
/// </summary>
[Requirement("REQ-10", "REQ-14", "REQ-37")]
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

    public MonitoringCycle(
        Func<MonitorSettings> settings,
        IClock clock,
        SiteLocationResolver siteResolver,
        OperatingWindow window,
        ImageAcquirer acquirer,
        ImageFreshnessEvaluator freshness,
        WeatherPoller weather,
        WeatherEvaluator weatherEvaluator)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.siteResolver = siteResolver ?? throw new ArgumentNullException(nameof(siteResolver));
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.acquirer = acquirer ?? throw new ArgumentNullException(nameof(acquirer));
        this.freshness = freshness ?? throw new ArgumentNullException(nameof(freshness));
        this.weather = weather ?? throw new ArgumentNullException(nameof(weather));
        this.weatherEvaluator = weatherEvaluator ?? throw new ArgumentNullException(nameof(weatherEvaluator));
    }

    /// <inheritdoc />
    public async Task<MonitorSnapshot> RunOnceAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        MonitorSettings s = settings();
        bool untuned = !s.Cloud.Tuned;

        SiteLocation site = siteResolver.Resolve();
        ConditionCheck windowCheck = window.Evaluate(site, clock.UtcNow, s.Window.SunAltitudeLimitDeg);

        if (!windowCheck.Passed)
        {
            var idle = new SafetyStatus(SafetyState.Idle, false, windowCheck.Reason, null, OverrideMode.Auto, untuned, clock.UtcNow);
            return new MonitorSnapshot(null, null, null, null, new SafetyConditions(new[] { windowCheck }), idle);
        }

        // Image and weather are independent, so fetch them together.
        await Task.WhenAll(acquirer.AcquireAsync(ct), weather.PollIfDueAsync(ct)).ConfigureAwait(false);

        DateTime now = clock.UtcNow;
        var checks = new List<ConditionCheck>
        {
            windowCheck,
            freshness.Evaluate(acquirer.LatestImage, acquirer.LastSuccessUtc, acquirer.LastError, now, s.ImageSource),
        };
        checks.AddRange(weatherEvaluator.Evaluate(weather.Latest, weather.LastError, now, s.Weather));
        checks.Add(new ConditionCheck(SafetyCondition.NoCloud, false, "Cloud detection not implemented yet"));

        // Distinct: with no weather data, rain, wind and data age all fail for the same reason.
        string reason = string.Join(" · ", checks.Where(c => !c.Passed).Select(c => c.Reason).Distinct());
        var status = new SafetyStatus(SafetyState.Unsafe, false, reason, null, OverrideMode.Auto, untuned, now);
        WeatherObservation? obs = s.Weather.Enabled ? weather.Latest : null;
        return new MonitorSnapshot(acquirer.LatestImage, null, null, obs, new SafetyConditions(checks), status);
    }
}
