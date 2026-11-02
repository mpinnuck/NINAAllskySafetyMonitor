using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Weather;

[Requirement("REQ-32")]
public sealed class WeatherPollerTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly WeatherSettings settings = new();
    private readonly FakeClock clock = new(Start);
    private readonly FakeWeatherProvider provider = new();
    private readonly ListLogSink log = new();

    private WeatherPoller Poller() => new(provider, clock, log, () => settings);

    [Fact]
    public async Task PollsAtTheWeatherIntervalNotEveryCycle()
    {
        provider.Then(Start, 0, 5);
        WeatherPoller p = Poller();

        await p.PollIfDueAsync(CancellationToken.None);           // 0 s: poll
        clock.UtcNow = Start.AddSeconds(60);
        await p.PollIfDueAsync(CancellationToken.None);           // 60 s: not due
        clock.UtcNow = Start.AddSeconds(120);
        await p.PollIfDueAsync(CancellationToken.None);           // 120 s: poll

        Assert.Equal(2, provider.Requests);
        Assert.NotNull(p.Latest);
    }

    [Fact]
    public async Task NothingIsPolledWhenWeatherCheckingIsOff()
    {
        settings.Enabled = false;
        await Poller().PollIfDueAsync(CancellationToken.None);
        Assert.Equal(0, provider.Requests);
    }

    [Fact]
    public async Task FailureKeepsLastObservationAndIsLoggedOnce()
    {
        provider.Then(Start, 0, 5).Then(WeatherFetchResult.Failed("Timed out"));
        WeatherPoller p = Poller();
        await p.PollIfDueAsync(CancellationToken.None);

        for (int i = 1; i <= 3; i++)
        {
            clock.UtcNow = Start.AddMinutes(2 * i);
            await p.PollIfDueAsync(CancellationToken.None);
        }

        Assert.NotNull(p.Latest);
        Assert.Equal("Timed out", p.LastError);
        Assert.Single(log.Entries, e => e.Message.Contains("Timed out"));
    }
}
