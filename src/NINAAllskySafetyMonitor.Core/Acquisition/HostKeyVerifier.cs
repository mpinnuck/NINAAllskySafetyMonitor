using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>Compares the Pi's host-key fingerprint with the pinned one.</summary>
[Requirement("REQ-52")]
public sealed class HostKeyVerifier
{
    private readonly string pinned;

    /// <param name="pinnedFingerprint">Pinned SHA256 fingerprint, with or without the "SHA256:" prefix.</param>
    public HostKeyVerifier(string pinnedFingerprint)
    {
        pinned = Normalise(pinnedFingerprint ?? string.Empty);
    }

    /// <summary>True only when <paramref name="receivedSha256"/> matches the pinned fingerprint exactly.</summary>
    public bool Verify(string? receivedSha256) =>
        pinned.Length > 0 && string.Equals(pinned, Normalise(receivedSha256 ?? string.Empty), StringComparison.Ordinal);

    /// <summary>Removes the "SHA256:" prefix, whitespace and base64 padding.</summary>
    internal static string Normalise(string fingerprint)
    {
        string f = fingerprint.Trim();
        if (f.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase)) f = f[7..];
        return f.Trim().TrimEnd('=');
    }
}
