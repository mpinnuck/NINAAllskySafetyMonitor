using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Decision;

/// <summary>
/// Unsafe at once; safe only after every condition has passed continuously for the safe-after period.
/// Any failed cycle restarts the period.
/// </summary>
[Requirement("REQ-38", "REQ-39")]
public sealed class SafetyHysteresis
{
    private DateTime? clearSinceUtc;

    /// <summary>When the current unbroken clear period began, or null.</summary>
    public DateTime? ClearSinceUtc => clearSinceUtc;

    /// <summary>Records a cycle. Returns whether it is safe and, while waiting, the seconds left.</summary>
    public (bool Safe, int? SecondsToSafe) Apply(bool allPassed, DateTime nowUtc, TimeSpan safeAfter)
    {
        if (!allPassed)
        {
            clearSinceUtc = null;
            return (false, null);
        }

        clearSinceUtc ??= nowUtc;
        TimeSpan remaining = safeAfter - (nowUtc - clearSinceUtc.Value);
        return remaining <= TimeSpan.Zero ? (true, null) : (false, (int)Math.Ceiling(remaining.TotalSeconds));
    }

    /// <summary>Forgets the clear period (for example after a failed or idle cycle).</summary>
    public void Reset() => clearSinceUtc = null;
}
