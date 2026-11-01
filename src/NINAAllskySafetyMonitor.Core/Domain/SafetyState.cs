namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Overall state shown to the user and reported to NINA.</summary>
public enum SafetyState
{
    /// <summary>Monitor connected, no decision made yet (reported unsafe).</summary>
    Starting,

    /// <summary>Outside the operating window (reported unsafe).</summary>
    Idle,

    /// <summary>All conditions held for the required clear period.</summary>
    Safe,

    /// <summary>At least one condition failed, or the clear period has not elapsed.</summary>
    Unsafe,
}
