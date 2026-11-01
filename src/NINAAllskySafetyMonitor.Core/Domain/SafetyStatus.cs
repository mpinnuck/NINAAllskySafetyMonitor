using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>The decision reported to NINA and shown to the user.</summary>
/// <param name="State">Overall state.</param>
/// <param name="IsSafe">Value reported as ISafetyMonitor.IsSafe.</param>
/// <param name="Reason">Why the monitor is in this state.</param>
/// <param name="SecondsToSafe">Countdown while waiting for the clear period, otherwise null.</param>
/// <param name="Override">Manual override in effect.</param>
/// <param name="Untuned">True while thresholds are still the untuned defaults.</param>
/// <param name="TimestampUtc">When the decision was made.</param>
[Requirement("REQ-03", "REQ-39", "REQ-40", "REQ-43")]
public sealed record SafetyStatus(
    SafetyState State,
    bool IsSafe,
    string Reason,
    int? SecondsToSafe,
    OverrideMode Override,
    bool Untuned,
    DateTime TimestampUtc)
{
    /// <summary>Status before the first decision: unsafe.</summary>
    public static SafetyStatus Starting(DateTime utc) =>
        new(SafetyState.Starting, false, "Starting", null, OverrideMode.Auto, false, utc);
}
