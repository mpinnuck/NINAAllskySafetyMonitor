using System.Globalization;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>
/// One-off fetch with settings that haven't been saved yet, for the "Test image" button on the
/// settings page. Reports the result in words.
/// </summary>
[Requirement("REQ-46")]
public sealed class ImageFetchTester
{
    private static readonly HttpClient Http = new();
    private readonly ICredentialStore credentials;
    private readonly ILogSink log;
    private readonly IClock clock;

    public ImageFetchTester(ICredentialStore credentials, ILogSink log, IClock clock)
    {
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Fetches once from the source selected in <paramref name="s"/> and describes the outcome.</summary>
    public async Task<string> TestAsync(ImageSourceSettings s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var sources = new ImageSourceFactory(
            new WebImageSource(Http, () => s),
            new SftpImageSource(new SftpConnector(credentials, log), () => s));

        FetchResult result = await sources.For(s.Kind).FetchAsync(true, ct).ConfigureAwait(false);
        if (result.Outcome == FetchOutcome.Failed || result.Image is null)
        {
            return $"Failed ({s.Kind}): {result.Error}";
        }

        double ageMin = Math.Max(0, (clock.UtcNow - result.Image.CaptureTimeUtc).TotalMinutes);
        bool complete = new JpegIntegrityChecker().IsComplete(result.Image.JpegBytes);
        return string.Create(
            CultureInfo.CurrentCulture,
            $"OK ({s.Kind}): {result.Image.JpegBytes.Length / 1024} KB, captured {result.Image.CaptureTimeUtc.ToLocalTime():HH:mm:ss} ({ageMin:0.0} min ago), {(complete ? "complete JPEG" : "INCOMPLETE JPEG")}");
    }
}
