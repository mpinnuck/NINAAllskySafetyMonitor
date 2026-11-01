using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>
/// Fetches the image file directly from the Allsky Pi. Checks the file's modification time first
/// and skips the download when it hasn't changed. The modification time is the capture time.
/// </summary>
[Requirement("REQ-11", "REQ-15", "REQ-16", "REQ-51")]
public sealed class SftpImageSource : IImageSource
{
    private readonly IRemoteFileConnector connector;
    private readonly Func<ImageSourceSettings> settings;
    private readonly object sync = new();
    private DateTime? lastModifiedUtc;

    public SftpImageSource(IRemoteFileConnector connector, Func<ImageSourceSettings> settings)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public ImageSourceKind Kind => ImageSourceKind.Direct;

    /// <inheritdoc />
    public async Task<FetchResult> FetchAsync(bool force, CancellationToken ct)
    {
        ImageSourceSettings s = settings();
        DirectSourceSettings direct = s.Direct;
        TimeSpan timeout = TimeSpan.FromSeconds(s.TimeoutS);

        // Leave room for the fallback address within the overall timeout.
        TimeSpan connectTimeout = TimeSpan.FromSeconds(Math.Max(2, s.TimeoutS / 2.0));

        try
        {
            return await Task.Run(() => Fetch(direct, force, connectTimeout), ct).WaitAsync(timeout, ct).ConfigureAwait(false);
        }
        catch (HostKeyMismatchException e)
        {
            return FetchResult.FailedUntrusted(e.Message);
        }
        catch (TimeoutException)
        {
            return FetchResult.Failed($"Timed out after {s.TimeoutS} s fetching the image from {direct.Host}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return FetchResult.Failed(e.Message);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (sync) lastModifiedUtc = null;
    }

    private FetchResult Fetch(DirectSourceSettings direct, bool force, TimeSpan connectTimeout)
    {
        using IRemoteFileSession session = connector.Connect(direct, connectTimeout);
        DateTime modified = DateTime.SpecifyKind(session.GetLastWriteTimeUtc(direct.RemotePath), DateTimeKind.Utc);

        lock (sync)
        {
            if (!force && lastModifiedUtc == modified) return FetchResult.Unchanged();
        }

        byte[] bytes = session.ReadAllBytes(direct.RemotePath);
        lock (sync) lastModifiedUtc = modified;
        return FetchResult.New(new SkyImage(bytes, modified, ImageSourceKind.Direct));
    }
}
