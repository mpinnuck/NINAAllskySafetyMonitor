using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Outcome of one safety condition, with a reason readable by the user.</summary>
/// <param name="Condition">The condition checked.</param>
/// <param name="Passed">True when the condition is satisfied.</param>
/// <param name="Reason">Short explanation, for example "image 12 min old".</param>
[Requirement("REQ-37", "REQ-50")]
public sealed record ConditionCheck(SafetyCondition Condition, bool Passed, string Reason)
{
    /// <summary>
    /// True when the condition failed because it could not be determined (no usable image, cloud unknown, no current weather data),
    /// rather than because something unsafe was detected. Such a failure may be treated as safe (REQ-50).
    /// </summary>
    public bool Undetermined { get; init; }

    /// <summary>A failed check for a condition that could not be determined.</summary>
    public static ConditionCheck Unknown(SafetyCondition condition, string reason) => new(condition, false, reason) { Undetermined = true };
}
