using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Mount source returning the state set by the test.</summary>
public sealed class FakeMountStateSource : IMountStateSource
{
    public MountState? Current { get; set; }

    public MountState? GetCurrent() => Current;

    /// <summary>A connected, unparked mount tracking at the given position (JNow).</summary>
    public static MountState Tracking(double raHours, double decDeg, bool slewing = false) =>
        new(true, false, true, slewing, raHours, decDeg, CoordinateEpoch.JNow, 45.0, 0.0, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
}
