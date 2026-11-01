namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Manual override used for testing (REQ-40).</summary>
public enum OverrideMode
{
    /// <summary>No override; the decision engine decides.</summary>
    Auto,

    /// <summary>Always report safe.</summary>
    ForceSafe,

    /// <summary>Always report unsafe.</summary>
    ForceUnsafe,
}
