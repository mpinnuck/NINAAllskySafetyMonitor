namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Where an Allsky image came from.</summary>
public enum ImageSourceKind
{
    /// <summary>Web server (HTTP/HTTPS).</summary>
    Web,

    /// <summary>Directly from the Allsky Pi over SFTP.</summary>
    Direct,

    /// <summary>A saved frame replayed offline.</summary>
    Replay,
}
