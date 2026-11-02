using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Weather;

[Requirement("REQ-33")]
public sealed class PwsObservationParserTests
{
    /// <summary>Shape of the ISYDNEY478 response confirmed on 2026-11-01 (values illustrative).</summary>
    internal const string Sample = """
        {"observations":[{"stationID":"ISYDNEY478","obsTimeUtc":"2026-11-01T10:00:00Z","obsTimeLocal":"2026-11-01 21:00:00",
          "neighborhood":"Hornsby","softwareType":"EasyWeatherPro","country":"AU","solarRadiation":0.0,"lon":151.09024,
          "realtimeFrequency":null,"epoch":1793527200,"lat":-33.713562,"uv":0.0,"winddir":180,"humidity":81.0,"qcStatus":1,
          "metric":{"temp":17.2,"heatIndex":17.2,"dewpt":13.9,"windChill":17.2,"windSpeed":3.6,"windGust":7.2,
                    "pressure":1016.9,"precipRate":0.0,"precipTotal":0.3,"elev":134.0}}]}
        """;

    private readonly PwsObservationParser parser = new();

    [Fact]
    public void ReadsEpochRainRateAndGust()
    {
        WeatherObservation o = parser.Parse(Sample);
        Assert.Equal(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc), o.ObservationTimeUtc);
        Assert.Equal(DateTimeKind.Utc, o.ObservationTimeUtc.Kind);
        Assert.Equal(0.0, o.PrecipRateMmH);
        Assert.Equal(7.2, o.WindGustKmH);
    }

    [Fact]
    public void UsesRainRateNotDailyTotal()
    {
        // precipTotal is 0.3 mm in the sample; only precipRate (0.0 mm/h) counts.
        Assert.Equal(0.0, parser.Parse(Sample).PrecipRateMmH);
    }

    [Fact]
    public void MissingGustIsAnError()
    {
        string json = Sample.Replace("\"windGust\":7.2", "\"windGust\":null");
        var e = Assert.Throws<FormatException>(() => parser.Parse(json));
        Assert.Contains("windGust", e.Message);
    }

    [Theory]
    [InlineData("{\"observations\":[]}", "no observations")]
    [InlineData("{}", "no observations")]
    [InlineData("not json", "not valid JSON")]
    public void UnusableResponsesAreErrors(string json, string expected)
    {
        var e = Assert.Throws<FormatException>(() => parser.Parse(json));
        Assert.Contains(expected, e.Message);
    }
}
