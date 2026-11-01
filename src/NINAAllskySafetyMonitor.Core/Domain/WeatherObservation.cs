using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Current observation from the personal weather station.</summary>
/// <param name="ObservationTimeUtc">Observation time (from the API's epoch field).</param>
/// <param name="PrecipRateMmH">Precipitation rate in mm/h.</param>
/// <param name="WindGustKmH">Wind gust in km/h.</param>
[Requirement("REQ-33")]
public sealed record WeatherObservation(DateTime ObservationTimeUtc, double PrecipRateMmH, double WindGustKmH);
