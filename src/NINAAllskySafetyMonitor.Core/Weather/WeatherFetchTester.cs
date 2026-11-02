using System.Globalization;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Weather;

/// <summary>One-off weather request with settings that haven't been saved yet, for the "Test weather" button.</summary>
[Requirement("REQ-46")]
public sealed class WeatherFetchTester
{
    private static readonly HttpClient Http = new();
    private readonly IClock clock;

    public WeatherFetchTester(IClock clock)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<string> TestAsync(WeatherSettings s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        WeatherFetchResult r = await new PwsWeatherClient(Http, () => s).GetLatestAsync(ct).ConfigureAwait(false);
        if (r.Observation is null) return $"Weather failed: {r.Error}";

        double age = Math.Max(0, (clock.UtcNow - r.Observation.ObservationTimeUtc).TotalMinutes);
        return string.Create(
            CultureInfo.CurrentCulture,
            $"Weather OK ({s.StationId}): rain {r.Observation.PrecipRateMmH:0.0} mm/h, gust {r.Observation.WindGustKmH:0} km/h, observed {r.Observation.ObservationTimeUtc.ToLocalTime():HH:mm} ({age:0.0} min ago)");
    }
}
