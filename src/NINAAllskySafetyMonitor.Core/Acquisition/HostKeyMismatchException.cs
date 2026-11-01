using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>The Pi presented a host key that does not match the pinned fingerprint.</summary>
[Requirement("REQ-52")]
public sealed class HostKeyMismatchException : Exception
{
    public HostKeyMismatchException(string host, string received)
        : base($"Host key mismatch for {host}: received {received}, which does not match the pinned fingerprint. Image treated as unavailable.")
    {
        Host = host;
        Received = received;
    }

    /// <summary>Host that was contacted.</summary>
    public string Host { get; }

    /// <summary>Key type and fingerprint actually received.</summary>
    public string Received { get; }
}
