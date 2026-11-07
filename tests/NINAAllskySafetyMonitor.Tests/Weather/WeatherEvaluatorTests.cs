using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Weather;

[Requirement("REQ-34", "REQ-35", "REQ-36", "REQ-50")]
public sealed class WeatherEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly WeatherSettings settings = new();
    private readonly WeatherEvaluator evaluator = new();

    private ConditionCheck Check(SafetyCondition c, WeatherObservation? o, string? error = null) =>
        evaluator.Evaluate(o, error, Now, settings).Single(x => x.Condition == c);

    private static WeatherObservation Obs(double rain = 0, double gust = 10, double minutesOld = 1) =>
        new(Now.AddMinutes(-minutesOld), rain, gust);

    [Fact]
    public void DryCalmCurrentWeatherPassesAllThree() =>
        Assert.All(evaluator.Evaluate(Obs(), null, Now, settings), c => Assert.True(c.Passed));

    [Theory]
    [InlineData(0.1)]
    [InlineData(2.5)]
    public void AnyRainIsUnsafe(double rate)
    {
        ConditionCheck c = Check(SafetyCondition.NoRain, Obs(rain: rate));
        Assert.False(c.Passed);
        Assert.False(c.Undetermined);
        Assert.StartsWith("Rain ", c.Reason);
    }

    [Fact]
    public void GustAboveLimitIsUnsafe()
    {
        ConditionCheck c = Check(SafetyCondition.WindBelowLimit, Obs(gust: 40.1));
        Assert.False(c.Passed);
        Assert.False(c.Undetermined);
        Assert.Contains("limit 40 km/h", c.Reason);
    }

    [Fact]
    public void GustAtLimitIsAccepted() => Assert.True(Check(SafetyCondition.WindBelowLimit, Obs(gust: 40)).Passed);

    [Fact]
    public void OldDataIsUnsafe()
    {
        ConditionCheck c = Check(SafetyCondition.WeatherCurrent, Obs(minutesOld: 16));
        Assert.False(c.Passed);
        Assert.False(c.Undetermined);
        Assert.Contains("16.0 min old (limit 15 min)", c.Reason);
    }

    [Fact]
    public void NoDataFailsAllThree()
    {
        IReadOnlyList<ConditionCheck> checks = evaluator.Evaluate(null, "API key was rejected", Now, settings);
        Assert.All(checks, c => Assert.False(c.Passed));
        Assert.All(checks, c => Assert.True(c.Undetermined));
        Assert.Contains(checks, c => c.Reason.Contains("API key was rejected"));
    }

    [Fact]
    public void NoWeatherDataPastStaleLimitIsKnownUnsafe()
    {
        IReadOnlyList<ConditionCheck> checks = evaluator.Evaluate(null, "API unavailable", Now, settings, Now.AddMinutes(-16));

        Assert.All(checks, c => Assert.False(c.Passed));
        Assert.All(checks, c => Assert.False(c.Undetermined));
        Assert.Contains(checks, c => c.Reason.Contains("No weather data for 16.0 min"));
    }

    [Fact]
    public void SwitchedOffPassesAllThree()
    {
        settings.Enabled = false;
        IReadOnlyList<ConditionCheck> checks = evaluator.Evaluate(null, null, Now, settings);
        Assert.Equal(3, checks.Count);
        Assert.All(checks, c => Assert.True(c.Passed));
    }
}
