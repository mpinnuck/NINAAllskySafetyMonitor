using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Decision;

/// <summary>
/// Turns a cycle's conditions into the decision: Idle outside the operating window; while the mount is
/// slewing, the last decision is held (Unsafe if there is none yet); Unsafe at once when any condition
/// fails (or none could be evaluated); Safe only after all conditions have held for the safe-after
/// period, counting down meanwhile. When enabled, genuinely undetermined conditions may be assumed
/// clear; known failures such as stale data or an invalid image, detected cloud, rain, wind, and a
/// monitor error stay unsafe (REQ-50).
/// </summary>
[Requirement("REQ-03", "REQ-27", "REQ-37", "REQ-38", "REQ-39", "REQ-50")]
public sealed class SafetyDecisionEngine
{
    private readonly SafetyHysteresis hysteresis = new();
    private readonly IClock clock;
    private readonly Func<TimeSpan> safeAfter;
    private readonly SlewHoldPolicy slewHold;
    private readonly Func<bool> undeterminedIsSafe;
    private SafetyStatus? lastDecided;

    public SafetyDecisionEngine(IClock clock, Func<TimeSpan> safeAfter, SlewHoldPolicy? slewHold = null, Func<bool>? undeterminedIsSafe = null)
    {
        this.undeterminedIsSafe = undeterminedIsSafe ?? (() => false);
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.safeAfter = safeAfter ?? throw new ArgumentNullException(nameof(safeAfter));
        this.slewHold = slewHold ?? new SlewHoldPolicy();
    }

    /// <summary>The decided status for a cycle's snapshot (before any manual override).</summary>
    public SafetyStatus Decide(MonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        DateTime now = clock.UtcNow;
        SafetyStatus s = snapshot.Status;

        if (s.State == SafetyState.Idle)
        {
            hysteresis.Reset();
            return Remember(s with { IsSafe = false, SecondsToSafe = null });
        }

        if (slewHold.ShouldHold(snapshot.Mount))
        {
            // The ROI is moving, so this cycle's view says nothing about the new target: keep the last
            // decision, and leave the clear-period timer as it was.
            if (lastDecided is { State: SafetyState.Safe or SafetyState.Unsafe } held)
            {
                return held with { Reason = $"Mount slewing: holding last decision ({held.Reason})", TimestampUtc = now };
            }

            return s with { State = SafetyState.Unsafe, IsSafe = false, SecondsToSafe = null, Reason = "Mount slewing", TimestampUtc = now };
        }

        bool assumed = !snapshot.Conditions.AllPassed && snapshot.Conditions.OnlyUndeterminedFailures && undeterminedIsSafe();
        if (!snapshot.Conditions.AllPassed && !assumed)
        {
            hysteresis.Reset();
            string reason = string.IsNullOrWhiteSpace(s.Reason)
                ? string.Join(" · ", snapshot.Conditions.Failures.Select(c => c.Reason).Distinct())
                : s.Reason;
            return Remember(s with { State = SafetyState.Unsafe, IsSafe = false, SecondsToSafe = null, Reason = reason.Length > 0 ? reason : "Conditions could not be determined" });
        }

        (bool safe, int? seconds) = hysteresis.Apply(true, now, safeAfter());
        string clear = assumed
            ? "Assumed safe: " + string.Join(" · ", snapshot.Conditions.Failures.Select(c => c.Reason).Distinct())
            : "All conditions clear";
        return Remember(safe
            ? s with { State = SafetyState.Safe, IsSafe = true, SecondsToSafe = null, Reason = clear, Assumed = assumed }
            : s with { State = SafetyState.Unsafe, IsSafe = false, SecondsToSafe = seconds, Reason = $"{clear}: safe in {Countdown(seconds!.Value)}", Assumed = assumed });
    }

    private SafetyStatus Remember(SafetyStatus s)
    {
        lastDecided = s;
        return s;
    }

    private static string Countdown(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
}
