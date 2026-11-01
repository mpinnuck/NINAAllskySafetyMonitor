using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>Provides the mount state as reported by the host (NINA).</summary>
[Requirement("REQ-05")]
public interface IMountStateSource
{
    /// <summary>Latest mount state, or null when no mount information is available.</summary>
    MountState? GetCurrent();
}
