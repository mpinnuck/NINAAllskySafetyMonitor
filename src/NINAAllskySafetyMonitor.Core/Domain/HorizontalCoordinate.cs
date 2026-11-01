namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Position on the local sky.</summary>
/// <param name="AltDeg">Altitude above the horizon in degrees.</param>
/// <param name="AzDeg">Azimuth in degrees, measured from north through east.</param>
public readonly record struct HorizontalCoordinate(double AltDeg, double AzDeg);
