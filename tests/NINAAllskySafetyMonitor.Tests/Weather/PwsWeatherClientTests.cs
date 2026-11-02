using System.Net;
using System.Text;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Weather;

[Requirement("REQ-32", "REQ-51", "REQ-54")]
public sealed class PwsWeatherClientTests
{
    private const string Key = "abc123secretkey";
    private readonly WeatherSettings settings = new() { ApiKey = Key };
    private readonly FakeHttpHandler server = new() { Body = Encoding.UTF8.GetBytes(PwsObservationParserTests.Sample) };

    private PwsWeatherClient Client() => new(new HttpClient(server), () => settings);

    [Fact]
    public async Task RequestsStationInMetricUnitsWithApiKey()
    {
        WeatherFetchResult r = await Client().GetLatestAsync(CancellationToken.None);

        Assert.NotNull(r.Observation);
        string url = server.LastRequest!.RequestUri!.ToString();
        Assert.StartsWith("https://api.weather.com/v2/pws/observations/current?", url);
        Assert.Contains("stationId=ISYDNEY478", url);
        Assert.Contains("units=m", url);
        Assert.Contains("apiKey=" + Key, url);
    }

    [Fact]
    public async Task NoContentMeansStationOffline()
    {
        server.Status = HttpStatusCode.NoContent;
        WeatherFetchResult r = await Client().GetLatestAsync(CancellationToken.None);
        Assert.Null(r.Observation);
        Assert.Contains("No current observation", r.Error);
    }

    [Fact]
    public async Task RejectedKeyIsReportedWithoutTheKey()
    {
        server.Status = HttpStatusCode.Unauthorized;
        WeatherFetchResult r = await Client().GetLatestAsync(CancellationToken.None);
        Assert.Contains("API key was rejected", r.Error);
        Assert.DoesNotContain(Key, r.Error);
    }

    [Fact]
    public async Task NetworkErrorNeverContainsTheKey()
    {
        server.Throw = true;
        server.ThrowMessage = $"Connection refused (api.weather.com:443) for ...apiKey={Key}";
        WeatherFetchResult r = await Client().GetLatestAsync(CancellationToken.None);
        Assert.Contains("Could not reach the weather API", r.Error);
        Assert.DoesNotContain(Key, r.Error);
    }

    [Fact]
    public async Task BadResponseIsAnErrorNotAnException()
    {
        server.Body = Encoding.UTF8.GetBytes("{\"observations\":[]}");
        WeatherFetchResult r = await Client().GetLatestAsync(CancellationToken.None);
        Assert.Null(r.Observation);
        Assert.Contains("no observations", r.Error);
    }
}
