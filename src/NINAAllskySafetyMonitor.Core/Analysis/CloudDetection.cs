using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>What <see cref="CloudDetector"/> found in one frame.</summary>
/// <param name="Assessment">The cloud assessment.</param>
/// <param name="CapturedReference">A clear-sky reference captured from this frame, to be saved; otherwise null.</param>
[Requirement("REQ-28", "REQ-30")]
public sealed record CloudDetection(CloudAssessment Assessment, ClearSkyReference? CapturedReference);
