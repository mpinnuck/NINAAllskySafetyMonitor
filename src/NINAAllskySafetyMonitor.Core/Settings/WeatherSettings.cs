using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Weather station settings (Weather.com PWS API).</summary>
[Requirement("REQ-32", "REQ-35", "REQ-36")]
public sealed class WeatherSettings
{
    /// <summary>When false, weather checks are ignored.</summary>
    public bool Enabled { get; set; } = true;

    public string StationId { get; set; } = "ISYDNEY478";

    /// <summary>The account's API key (not the device/station upload key). Never logged (REQ-54).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int PollIntervalS { get; set; } = 120;
    public double MaxGustKmH { get; set; } = 40.0;
    public int StaleAfterMin { get; set; } = 15;

    /// <summary>Timeout for one API request, in seconds (REQ-51).</summary>
    public int TimeoutS { get; set; } = 15;
}
