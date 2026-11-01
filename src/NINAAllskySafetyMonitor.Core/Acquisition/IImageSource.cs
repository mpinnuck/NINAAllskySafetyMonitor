using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>A place the latest Allsky image can be fetched from (web server or the Pi over SFTP).</summary>
[Requirement("REQ-11")]
public interface IImageSource
{
    /// <summary>Which source this is.</summary>
    ImageSourceKind Kind { get; }

    /// <summary>
    /// Fetches the latest image. Returns <see cref="FetchOutcome.Unchanged"/> when the image has not
    /// changed since the last fetch, unless <paramref name="force"/> is true. Never throws for network
    /// or server errors (those give <see cref="FetchOutcome.Failed"/>); throws only on cancellation.
    /// </summary>
    Task<FetchResult> FetchAsync(bool force, CancellationToken ct);

    /// <summary>Forgets the last image's timestamp, so the next fetch downloads again.</summary>
    void Reset();
}
