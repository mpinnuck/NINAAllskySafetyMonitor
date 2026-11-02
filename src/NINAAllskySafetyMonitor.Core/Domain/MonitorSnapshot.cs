using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Everything produced by one monitoring cycle, for the decision, the UI, the log and tuning.</summary>
[Requirement("REQ-41", "REQ-45")]
public sealed record MonitorSnapshot(
    SkyImage? Image,
    RegionOfInterest? Roi,
    CloudAssessment? Cloud,
    WeatherObservation? Weather,
    SafetyConditions Conditions,
    SafetyStatus Status,
    MountState? Mount = null);
