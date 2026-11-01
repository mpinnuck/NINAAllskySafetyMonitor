using NINAAllskySafetyMonitor.Core.Abstractions;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Clock whose time the test sets.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }
}
