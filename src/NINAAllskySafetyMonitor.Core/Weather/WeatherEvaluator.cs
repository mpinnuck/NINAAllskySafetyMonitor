using System.Globalization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Weather;

/// <summary>
/// The three weather conditions: NoRain (any rain rate above zero fails), WindBelowLimit (gust above
/// the limit fails) and WeatherCurrent (missing data, or data older than the limit, fails). With no
/// usable data all three fail. With weather checking switched off all three pass.
/// </summary>
[Requirement("REQ-34", "REQ-35", "REQ-36", "REQ-50")]
public sealed class WeatherEvaluator
{
    public IReadOnlyList<ConditionCheck> Evaluate(WeatherObservation? obs, string? lastError, DateTime nowUtc, WeatherSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);

        if (!s.Enabled)
        {
            return new[]
            {
                new ConditionCheck(SafetyCondition.NoRain, true, "Weather checking off"),
                new ConditionCheck(SafetyCondition.WindBelowLimit, true, "Weather checking off"),
                new ConditionCheck(SafetyCondition.WeatherCurrent, true, "Weather checking off"),
            };
        }

        if (obs is null)
        {
            string why = lastError is null ? "No weather data yet" : $"No weather data: {lastError}";
            return new[]
            {
                new ConditionCheck(SafetyCondition.NoRain, false, why),
                new ConditionCheck(SafetyCondition.WindBelowLimit, false, why),
                new ConditionCheck(SafetyCondition.WeatherCurrent, false, why),
            };
        }

        double age = Math.Max(0, (nowUtc - obs.ObservationTimeUtc).TotalMinutes);
        bool current = age <= s.StaleAfterMin;
        string failed = lastError is null ? string.Empty : $" (last poll failed: {lastError})";

        return new[]
        {
            obs.PrecipRateMmH > 0
                ? new ConditionCheck(SafetyCondition.NoRain, false, F($"Rain {obs.PrecipRateMmH:0.0} mm/h"))
                : new ConditionCheck(SafetyCondition.NoRain, true, "No rain"),
            obs.WindGustKmH > s.MaxGustKmH
                ? new ConditionCheck(SafetyCondition.WindBelowLimit, false, F($"Gust {obs.WindGustKmH:0} km/h (limit {s.MaxGustKmH:0} km/h)"))
                : new ConditionCheck(SafetyCondition.WindBelowLimit, true, F($"Gust {obs.WindGustKmH:0} km/h")),
            current
                ? new ConditionCheck(SafetyCondition.WeatherCurrent, true, F($"Weather {age:0.0} min old{failed}"))
                : new ConditionCheck(SafetyCondition.WeatherCurrent, false, F($"Weather data is {age:0.0} min old (limit {s.StaleAfterMin} min){failed}")),
        };
    }

    private static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
