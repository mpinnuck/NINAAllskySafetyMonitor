using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Result of the cloud tests for one image.</summary>
/// <param name="ExpectedStars">Catalogue stars expected in the ROI.</param>
/// <param name="DetectedStars">Expected stars that were found in the image.</param>
/// <param name="StarFraction">DetectedStars ÷ ExpectedStars, or null when inconclusive.</param>
/// <param name="BrightnessRatio">ROI median ÷ clear-sky reference, or null when no reference exists.</param>
/// <param name="UniformityRatio">ROI non-uniformity ÷ clear-sky reference, or null when no reference exists.</param>
/// <param name="StarTestInconclusive">True when too few stars were expected for the star test.</param>
/// <param name="CloudPresent">True when cloud was detected, or when neither test could run.</param>
[Requirement("REQ-28")]
public sealed record CloudAssessment(
    int ExpectedStars,
    int DetectedStars,
    double? StarFraction,
    double? BrightnessRatio,
    double? UniformityRatio,
    bool StarTestInconclusive,
    bool CloudPresent);
