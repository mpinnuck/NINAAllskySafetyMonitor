using System.Net;
using System.Net.Http.Headers;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>
/// Fetches the image from the Allsky web site (base URL + image file name). The server's
/// last-modified time is the capture time; without it the age can't be known, so the fetch fails.
/// </summary>
[Requirement("REQ-11", "REQ-15", "REQ-51")]
public sealed class WebImageSource : IImageSource
{
    private readonly HttpClient http;
    private readonly Func<ImageSourceSettings> settings;
    private readonly object sync = new();
    private DateTime? lastModifiedUtc;

    public WebImageSource(HttpClient http, Func<ImageSourceSettings> settings)
    {
        this.http = http ?? throw new ArgumentNullException(nameof(http));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public ImageSourceKind Kind => ImageSourceKind.Web;

    /// <summary>The image URL: base URL (with a trailing slash added if missing) plus file name.</summary>
    public static Uri ImageUrl(WebSourceSettings web)
    {
        ArgumentNullException.ThrowIfNull(web);
        string baseUrl = web.BaseUrl.Trim();
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        return new Uri(new Uri(baseUrl, UriKind.Absolute), web.FileName.Trim());
    }

    /// <inheritdoc />
    public async Task<FetchResult> FetchAsync(bool force, CancellationToken ct)
    {
        ImageSourceSettings s = settings();
        Uri url;
        try
        {
            url = ImageUrl(s.Web);
        }
        catch (Exception e) when (e is UriFormatException or ArgumentException)
        {
            return FetchResult.Failed($"Invalid web image URL: {e.Message}");
        }

        DateTime? known;
        lock (sync) known = lastModifiedUtc;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        if (!force && known is DateTime k) request.Headers.IfModifiedSince = new DateTimeOffset(k);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(s.TimeoutS));

        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified) return FetchResult.Unchanged();
            if (!response.IsSuccessStatusCode)
            {
                return FetchResult.Failed($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} from {url}");
            }

            DateTimeOffset? lastModified = response.Content.Headers.LastModified;
            if (lastModified is null)
            {
                return FetchResult.Failed($"The web server gave no last-modified time for {url}, so the image age is unknown");
            }

            DateTime modified = lastModified.Value.UtcDateTime;
            if (!force && known == modified) return FetchResult.Unchanged();

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
            lock (sync) lastModifiedUtc = modified;
            return FetchResult.New(new SkyImage(bytes, modified, ImageSourceKind.Web));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return FetchResult.Failed($"Timed out after {s.TimeoutS} s fetching {url}");
        }
        catch (HttpRequestException e)
        {
            return FetchResult.Failed($"Could not fetch {url}: {e.Message}");
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (sync) lastModifiedUtc = null;
    }
}
