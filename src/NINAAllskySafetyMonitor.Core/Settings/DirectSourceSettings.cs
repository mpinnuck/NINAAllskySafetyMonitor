using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>
/// Direct image source: SFTP from the Allsky Pi. A password, if used, is kept in the
/// credential store and never in this class or the settings file (REQ-53).
/// </summary>
[Requirement("REQ-11", "REQ-12", "REQ-13", "REQ-52")]
public sealed class DirectSourceSettings
{
    public string Host { get; set; } = "allsky.local";
    public string FallbackIp { get; set; } = "192.168.1.110";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "pi";
    public string RemotePath { get; set; } = "/home/pi/allsky/tmp/allsky.jpg";

    /// <summary>Private key file. Empty when password authentication is used.</summary>
    public string KeyFile { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_ed25519");

    /// <summary>When true, authenticate with the password held in the credential store.</summary>
    public bool UsePassword { get; set; }

    /// <summary>
    /// Host-key type the pinned fingerprint belongs to. The Pi has one key per type (RSA, ECDSA,
    /// Ed25519), so only this type is requested and compared.
    /// </summary>
    public string HostKeyAlgorithm { get; set; } = "ecdsa-sha2-nistp256";

    /// <summary>Pinned SHA256 host-key fingerprint of the Pi.</summary>
    public string HostKeyFingerprint { get; set; } = "SHA256:xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg";
}
