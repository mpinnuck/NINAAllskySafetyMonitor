namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>Clock backed by the system time.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
