using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>All condition checks for one monitoring cycle.</summary>
/// <param name="Checks">One check per condition.</param>
[Requirement("REQ-37")]
public sealed record SafetyConditions(IReadOnlyList<ConditionCheck> Checks)
{
    /// <summary>True only when there is at least one check and every check passed.</summary>
    public bool AllPassed => Checks.Count > 0 && Checks.All(c => c.Passed);

    /// <summary>The checks that failed.</summary>
    public IEnumerable<ConditionCheck> Failures => Checks.Where(c => !c.Passed);
}
