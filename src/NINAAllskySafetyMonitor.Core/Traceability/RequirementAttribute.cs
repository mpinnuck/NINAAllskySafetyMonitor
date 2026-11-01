namespace NINAAllskySafetyMonitor.Core.Traceability;

/// <summary>
/// Links a class, method or test to the requirements it implements or verifies
/// (requirements.md v4). Read by the requirement-coverage tests and the traceability report.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface |
    AttributeTargets.Enum | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = false)]
public sealed class RequirementAttribute : Attribute
{
    /// <summary>Creates the attribute with one or more requirement identifiers, e.g. "REQ-23".</summary>
    public RequirementAttribute(params string[] ids)
    {
        Ids = ids ?? Array.Empty<string>();
    }

    /// <summary>Requirement identifiers in the form "REQ-nn".</summary>
    public string[] Ids { get; }
}
