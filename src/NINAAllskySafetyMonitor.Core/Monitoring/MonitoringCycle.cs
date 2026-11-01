using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>
/// One monitoring pass (UC-04). Outside the operating window the status is Idle and nothing is
/// fetched. Inside it, the latest image is fetched and checked for freshness. Until cloud detection
/// is added (slice 6) the status stays Unsafe even with a fresh image. Settings are read at the start
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

    public MonitoringCycle(
        Func<MonitorSettings> settings,
        IClock clock,
        SiteLocationResolver siteResolver,
        OperatingWindow window,
        ImageAcquirer acquirer,
        ImageFreshnessEvaluator freshness)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.siteResolver = siteResolver ?? throw new ArgumentNullException(nameof(siteResolver));
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.acquirer = acquirer ?? throw new ArgumentNullException(nameof(acquirer));
        this.freshness = freshness ?? throw new ArgumentNullException(nameof(freshness));
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

        await acquirer.AcquireAsync(ct).ConfigureAwait(false);
        DateTime now = clock.UtcNow;
        ConditionCheck fresh = freshness.Evaluate(acquirer.LatestImage, acquirer.LastSuccessUtc, acquirer.LastError, now, s.ImageSource);

        var checks = new List<ConditionCheck> { windowCheck, fresh };
        string reason;
        if (fresh.Passed)
        {
            var cloudNotYet = new ConditionCheck(SafetyCondition.NoCloud, false, "Cloud detection not implemented yet");
            checks.Add(cloudNotYet);
            reason = $"{cloudNotYet.Reason} · {fresh.Reason}";
        }
        else
        {
            reason = fresh.Reason;
        }

        var status = new SafetyStatus(SafetyState.Unsafe, false, reason, null, OverrideMode.Auto, untuned, now);
        return new MonitorSnapshot(acquirer.LatestImage, null, null, null, new SafetyConditions(checks), status);
    }
}
