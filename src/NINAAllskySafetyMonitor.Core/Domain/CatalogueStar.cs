namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>A star from the embedded bright star catalogue (J2000).</summary>
/// <param name="Id">Catalogue number.</param>
/// <param name="RaHours">Right ascension in hours (J2000).</param>
/// <param name="DecDeg">Declination in degrees (J2000).</param>
/// <param name="Magnitude">Visual magnitude.</param>
public sealed record CatalogueStar(int Id, double RaHours, double DecDeg, double Magnitude);
