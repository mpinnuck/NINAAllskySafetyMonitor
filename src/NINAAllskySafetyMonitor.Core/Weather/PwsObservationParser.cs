using System.Text.Json;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Weather;

/// <summary>
/// Reads observations[0] of a Weather.com PWS current-observations response (metric units):
/// epoch (observation time), metric.precipRate (mm/h) and metric.windGust (km/h).
/// </summary>
[Requirement("REQ-33")]
public sealed class PwsObservationParser
{
    /// <summary>Parses the response; throws <see cref="FormatException"/> if a required value is missing.</summary>
    public WeatherObservation Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new FormatException($"Weather response is not valid JSON: {e.Message}", e);
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("observations", out JsonElement list)
                || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
            {
                throw new FormatException("Weather response has no observations");
            }

            JsonElement obs = list[0];
            long epoch = (long)Value(obs, "epoch", "epoch");
            if (!obs.TryGetProperty("metric", out JsonElement metric) || metric.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Weather response has no metric values (units=m expected)");
            }

            double rate = Value(metric, "precipRate", "metric.precipRate");
            double gust = Value(metric, "windGust", "metric.windGust");
            return new WeatherObservation(DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime, rate, gust);
        }
    }

    private static double Value(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number
            ? e.GetDouble()
            : throw new FormatException($"Weather response has no {path} value");
}
