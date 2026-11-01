using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>
/// One monitoring pass (UC-04). Slice 1 evaluates the operating window only: outside the
/// window the status is Idle; inside it, the status is Unsafe until image acquisition and the
/// remaining checks are added in later slices. Settings are read at the start of every cycle,
/// so saved changes take effect without reconnecting.
/// </summary>
[Requirement("REQ-10", "REQ-37")]
public sealed class MonitoringCycle : IMonitoringCycle
{
    private readonly Func<MonitorSettings> settings;
    private readonly IClock clock;
    private readonly SiteLocationResolver siteResolver;
    private readonly OperatingWindow window;

    public MonitoringCycle(Func<MonitorSettings> settings, IClock clock, SiteLocationResolver siteResolver, OperatingWindow window)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.siteResolver = siteResolver ?? throw new ArgumentNullException(nameof(siteResolver));
        this.window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <inheritdoc />
    public Task<MonitorSnapshot> RunOnceAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        MonitorSettings s = settings();
        DateTime now = clock.UtcNow;
        bool untuned = !s.Cloud.Tuned;

        SiteLocation site = siteResolver.Resolve();
        ConditionCheck windowCheck = window.Evaluate(site, now, s.Window.SunAltitudeLimitDeg);

        if (!windowCheck.Passed)
        {
            var idle = new SafetyStatus(SafetyState.Idle, false, windowCheck.Reason, null, OverrideMode.Auto, untuned, now);
            return Task.FromResult(new MonitorSnapshot(null, null, null, null, new SafetyConditions(new[] { windowCheck }), idle));
        }

        var notYet = new ConditionCheck(SafetyCondition.FreshImage, false, "Image acquisition not implemented yet");
        var conditions = new SafetyConditions(new[] { windowCheck, notYet });
        var unsafeStatus = new SafetyStatus(SafetyState.Unsafe, false, notYet.Reason, null, OverrideMode.Auto, untuned, now);
        return Task.FromResult(new MonitorSnapshot(null, null, null, null, conditions, unsafeStatus));
    }
}
