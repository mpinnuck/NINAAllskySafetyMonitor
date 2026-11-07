using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>
/// Fetches the latest image once per monitoring cycle from the selected source. An incomplete JPEG
/// is discarded and fetched again after a short wait, up to the configured number of attempts.
/// Keeps the latest good image and the time of the last successful fetch for the freshness check.
/// </summary>
[Requirement("REQ-14", "REQ-17", "REQ-18")]
public sealed class ImageAcquirer
{
    private readonly ImageSourceFactory sources;
    private readonly JpegIntegrityChecker checker;
    private readonly IClock clock;
    private readonly ILogSink log;
    private readonly Func<ImageSourceSettings> settings;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private ImageSourceKind? lastKind;
    private string? loggedError;

    public ImageAcquirer(
        ImageSourceFactory sources,
        JpegIntegrityChecker checker,
        IClock clock,
        ILogSink log,
        Func<ImageSourceSettings> settings,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
        this.checker = checker ?? throw new ArgumentNullException(nameof(checker));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.delay = delay ?? Task.Delay;
    }

    /// <summary>Latest complete image, or null if none has been fetched yet.</summary>
    public SkyImage? LatestImage { get; private set; }

    /// <summary>When the source was last reached successfully (new or unchanged image).</summary>
    public DateTime? LastSuccessUtc { get; private set; }

    /// <summary>When image acquisition first started, used to fail closed after an initial outage.</summary>
    public DateTime? FirstAttemptUtc { get; private set; }

    /// <summary>Reason the last fetch failed, or null if it succeeded.</summary>
    public string? LastError { get; private set; }

    /// <summary>Fetches from the selected source and updates the latest image.</summary>
    public async Task AcquireAsync(CancellationToken ct)
    {
        FirstAttemptUtc ??= clock.UtcNow;
        ImageSourceSettings s = settings();
        IImageSource source = sources.For(s.Kind);

        // A newly selected source must download, even if its image hasn't changed since it was last used.
        bool force = lastKind != s.Kind;
        lastKind = s.Kind;

        int attempts = Math.Max(1, s.IncompleteRetries);
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            FetchResult result = await source.FetchAsync(force || attempt > 1, ct).ConfigureAwait(false);

            switch (result.Outcome)
            {
                case FetchOutcome.Failed:
                    if (result.Untrusted)
                    {
                        // The Pi can't be trusted, so neither can images fetched from it earlier.
                        LatestImage = null;
                        LastSuccessUtc = null;
                    }

                    Fail(result.Error ?? "Image fetch failed");
                    return;

                case FetchOutcome.Unchanged:
                    Succeed();
                    return;

                case FetchOutcome.NewImage when checker.IsComplete(result.Image!.JpegBytes):
                    LatestImage = result.Image;
                    Succeed();
                    return;

                default:
                    log.Write(LogLevel.Info, $"Incomplete image (attempt {attempt} of {attempts}); it was probably still being written");
                    if (attempt < attempts)
                    {
                        await delay(TimeSpan.FromSeconds(s.IncompleteRetryS), ct).ConfigureAwait(false);
                    }

                    break;
            }
        }

        // Make sure the next cycle downloads again rather than reporting the same file as unchanged.
        source.Reset();
        Fail($"Image still incomplete after {attempts} attempts");
    }

    private void Succeed()
    {
        LastSuccessUtc = clock.UtcNow;
        LastError = null;
        if (loggedError is not null)
        {
            log.Write(LogLevel.Info, "Image fetch working again");
            loggedError = null;
        }
    }

    private void Fail(string error)
    {
        LastError = error;

        // Log each distinct error once, not every cycle.
        if (error != loggedError)
        {
            log.Write(LogLevel.Warning, $"Image fetch failed: {error}");
            loggedError = error;
        }
    }
}
