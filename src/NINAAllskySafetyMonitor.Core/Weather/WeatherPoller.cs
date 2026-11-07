using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Weather;

/// <summary>
/// Polls the weather provider at the weather interval, independently of the image interval: it is
/// asked every monitoring cycle and only polls when the interval has passed. Keeps the latest observation.
/// </summary>
[Requirement("REQ-32")]
public sealed class WeatherPoller
{
    private readonly IWeatherProvider provider;
    private readonly IClock clock;
    private readonly ILogSink log;
    private readonly Func<WeatherSettings> settings;
    private DateTime? lastPollUtc;
    private string? loggedError;

    public WeatherPoller(IWeatherProvider provider, IClock clock, ILogSink log, Func<WeatherSettings> settings)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Latest observation received, or null.</summary>
    public WeatherObservation? Latest { get; private set; }

    /// <summary>When weather polling first started, used to fail closed after an initial outage.</summary>
    public DateTime? FirstPollUtc { get; private set; }

    /// <summary>Reason the last poll failed, or null if it succeeded.</summary>
    public string? LastError { get; private set; }

    /// <summary>Polls if weather checking is on and the poll interval has passed since the last poll.</summary>
    public async Task PollIfDueAsync(CancellationToken ct)
    {
        WeatherSettings s = settings();
        if (!s.Enabled) return;

        DateTime now = clock.UtcNow;
        if (lastPollUtc is DateTime last && now - last < TimeSpan.FromSeconds(s.PollIntervalS)) return;
        FirstPollUtc ??= now;
        lastPollUtc = now;

        WeatherFetchResult result = await provider.GetLatestAsync(ct).ConfigureAwait(false);
        if (result.Observation is not null)
        {
            Latest = result.Observation;
            LastError = null;
            if (loggedError is not null)
            {
                log.Write(LogLevel.Info, "Weather data working again");
                loggedError = null;
            }

            return;
        }

        LastError = result.Error ?? "Weather request failed";
        if (LastError != loggedError)
        {
            log.Write(LogLevel.Warning, $"Weather fetch failed: {LastError}");
            loggedError = LastError;
        }
    }
}
