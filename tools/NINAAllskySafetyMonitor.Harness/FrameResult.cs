using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>Everything judged in one frame.</summary>
/// <param name="Frame">The frame.</param>
/// <param name="SunAltDeg">Sun altitude at the capture time.</param>
/// <param name="IsNight">True when the Sun was below the limit, so the frame was judged.</param>
/// <param name="SkyFrame">The sky-frame check, or null for a daylight frame.</param>
/// <param name="Targets">The targets judged.</param>
/// <param name="ReferenceCaptured">True when this frame became the clear-sky reference.</param>
public sealed record FrameResult(
    SavedFrame Frame,
    double SunAltDeg,
    bool IsNight,
    ConditionCheck? SkyFrame,
    IReadOnlyList<TargetResult> Targets,
    bool ReferenceCaptured);
