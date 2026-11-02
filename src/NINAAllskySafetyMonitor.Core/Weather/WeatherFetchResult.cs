using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Core.Weather;

/// <summary>Result of one weather request: an observation, or a readable error (never containing the API key).</summary>
public sealed record WeatherFetchResult(WeatherObservation? Observation, string? Error)
{
    public static WeatherFetchResult Ok(WeatherObservation observation) => new(observation, null);

    public static WeatherFetchResult Failed(string error) => new(null, error);
}
