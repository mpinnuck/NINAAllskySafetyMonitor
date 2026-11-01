namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Outcome of one safety condition, with a reason readable by the user.</summary>
/// <param name="Condition">The condition checked.</param>
/// <param name="Passed">True when the condition is satisfied.</param>
/// <param name="Reason">Short explanation, for example "image 12 min old".</param>
public sealed record ConditionCheck(SafetyCondition Condition, bool Passed, string Reason);
