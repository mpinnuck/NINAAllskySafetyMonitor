namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>Source of the current time, replaceable for tests and offline replay.</summary>
public interface IClock
{
    /// <summary>Current time in UTC.</summary>
    DateTime UtcNow { get; }
}
