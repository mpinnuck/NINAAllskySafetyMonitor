using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Domain;

[Requirement("REQ-03", "REQ-50")]
public sealed class SafetyStatusTests
{
    [Fact]
    public void StartingStatusIsUnsafe()
    {
        SafetyStatus status = SafetyStatus.Starting(DateTime.UtcNow);
        Assert.Equal(SafetyState.Starting, status.State);
        Assert.False(status.IsSafe);
        Assert.Equal(OverrideMode.Auto, status.Override);
    }
}
