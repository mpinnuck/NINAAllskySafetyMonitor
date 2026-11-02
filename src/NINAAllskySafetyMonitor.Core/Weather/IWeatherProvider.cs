using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Weather;

/// <summary>Source of the current weather observation for the site.</summary>
[Requirement("REQ-32")]
public interface IWeatherProvider
{
    /// <summary>Fetches the current observation. Never throws for network or server errors; throws only on cancellation.</summary>
    Task<WeatherFetchResult> GetLatestAsync(CancellationToken ct);
}
