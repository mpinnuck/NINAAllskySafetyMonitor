using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>One target judged in one frame.</summary>
public sealed record TargetResult(ReplayTarget Target, RegionOfInterest Roi, CloudAssessment Cloud)
{
    public Verdict Verdict => Cloud.Undetermined ? Verdict.Unknown : Cloud.CloudPresent ? Verdict.Cloud : Verdict.Clear;
}
