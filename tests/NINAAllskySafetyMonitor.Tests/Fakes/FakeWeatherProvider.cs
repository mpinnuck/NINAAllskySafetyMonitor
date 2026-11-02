using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Weather;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Weather provider returning queued results; repeats the last one when the queue runs out.</summary>
public sealed class FakeWeatherProvider : IWeatherProvider
{
    private readonly Queue<WeatherFetchResult> results = new();
    private WeatherFetchResult last = WeatherFetchResult.Failed("No result queued");

    public int Requests { get; private set; }

    public FakeWeatherProvider Then(WeatherFetchResult result)
    {
        results.Enqueue(result);
        return this;
    }

    public FakeWeatherProvider Then(DateTime observedUtc, double rainMmH, double gustKmH) =>
        Then(WeatherFetchResult.Ok(new WeatherObservation(observedUtc, rainMmH, gustKmH)));

    public Task<WeatherFetchResult> GetLatestAsync(CancellationToken ct)
    {
        Requests++;
        if (results.Count > 0) last = results.Dequeue();
        return Task.FromResult(last);
    }
}
