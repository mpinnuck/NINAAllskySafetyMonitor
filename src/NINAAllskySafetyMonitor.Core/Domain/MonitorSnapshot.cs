using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Everything produced by one monitoring cycle, for the decision, the UI, the log and tuning.</summary>
[Requirement("REQ-41", "REQ-45", "REQ-60")]
public sealed record MonitorSnapshot(
    SkyImage? Image,
    RegionOfInterest? Roi,
    CloudAssessment? Cloud,
    WeatherObservation? Weather,
    SafetyConditions Conditions,
    SafetyStatus Status,
    MountState? Mount = null)
{
    /// <summary>
    /// True when the image was judged not to be a sky frame (REQ-60), for example Allsky's notice image,
    /// so the UI shows it without the sky overlay.
    /// </summary>
    public bool NotASkyFrame { get; init; }
}
