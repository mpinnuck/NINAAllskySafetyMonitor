using System.Net;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Weather;

/// <summary>
/// Weather.com PWS current-observations API, metric units, authenticated with the account API key.
/// The key is sent in the query string, so error messages use a URL with the key removed.
/// </summary>
[Requirement("REQ-32", "REQ-51", "REQ-54")]
public sealed class PwsWeatherClient : IWeatherProvider
{
    private const string Endpoint = "https://api.weather.com/v2/pws/observations/current";
    private readonly HttpClient http;
    private readonly Func<WeatherSettings> settings;
    private readonly PwsObservationParser parser = new();

    public PwsWeatherClient(HttpClient http, Func<WeatherSettings> settings)
    {
        this.http = http ?? throw new ArgumentNullException(nameof(http));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Request URL including the API key. Never log this.</summary>
    internal static Uri RequestUrl(WeatherSettings s) =>
        new($"{Endpoint}?stationId={Uri.EscapeDataString(s.StationId.Trim())}&format=json&units=m&numericPrecision=decimal&apiKey={Uri.EscapeDataString(s.ApiKey.Trim())}");

    /// <summary>What to show in messages: the station, never the key.</summary>
    internal static string Describe(WeatherSettings s) => $"station {s.StationId.Trim()}";

    /// <inheritdoc />
    public async Task<WeatherFetchResult> GetLatestAsync(CancellationToken ct)
    {
        WeatherSettings s = settings();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(s.TimeoutS));

        try
        {
            using HttpResponseMessage response = await http.GetAsync(RequestUrl(s), timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return WeatherFetchResult.Failed($"No current observation from {Describe(s)} (station offline?)");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return WeatherFetchResult.Failed($"Weather API key was rejected (HTTP {(int)response.StatusCode})");
            }

            if (!response.IsSuccessStatusCode)
            {
                return WeatherFetchResult.Failed($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} from the weather API for {Describe(s)}");
            }

            string json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            return WeatherFetchResult.Ok(parser.Parse(json));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return WeatherFetchResult.Failed($"Timed out after {s.TimeoutS} s fetching weather for {Describe(s)}");
        }
        catch (HttpRequestException e)
        {
            return WeatherFetchResult.Failed($"Could not reach the weather API: {Redact(e.Message, s)}");
        }
        catch (FormatException e)
        {
            return WeatherFetchResult.Failed(e.Message);
        }
    }

    /// <summary>Removes the API key from any text, in case a library includes the URL in a message.</summary>
    internal static string Redact(string text, WeatherSettings s) =>
        string.IsNullOrEmpty(s.ApiKey) ? text : text.Replace(s.ApiKey.Trim(), "***", StringComparison.Ordinal);
}
