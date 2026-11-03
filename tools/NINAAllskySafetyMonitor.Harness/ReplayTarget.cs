namespace NINAAllskySafetyMonitor.Harness;

/// <summary>A fixed sky position followed through the night, like a target the mount tracks.</summary>
/// <param name="Id">Short name, for example "RA05 Dec-35".</param>
/// <param name="RaHours">Right ascension (J2000).</param>
/// <param name="DecDeg">Declination (J2000).</param>
public sealed record ReplayTarget(string Id, double RaHours, double DecDeg);
