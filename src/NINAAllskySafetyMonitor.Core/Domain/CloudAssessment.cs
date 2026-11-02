using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Result of the cloud tests for one image.</summary>
/// <param name="ExpectedStars">Catalogue stars expected in the ROI.</param>
/// <param name="DetectedStars">Expected stars that were found in the image.</param>
/// <param name="StarFraction">DetectedStars ÷ ExpectedStars, or null when no star was expected.</param>
/// <param name="RelativeBrightness">
/// ROI brightness relative to the rest of the working area, both compared with the clear-sky reference
/// (1.0 = as in clear sky), or null when the brightness test was skipped.
/// </param>
/// <param name="UniformityRatio">ROI non-uniformity ÷ clear-sky reference, or null when the brightness test was skipped.</param>
/// <param name="StarTestInconclusive">True when too few stars were expected for the star test.</param>
/// <param name="BrightnessTestSkipped">True when there is no clear-sky reference to compare with.</param>
/// <param name="CloudPresent">True when cloud was detected, or when neither test could run.</param>
/// <param name="Summary">Short explanation, for example "Cloud: 3 of 11 stars (27%)".</param>
[Requirement("REQ-28", "REQ-45")]
public sealed record CloudAssessment(
    int ExpectedStars,
    int DetectedStars,
    double? StarFraction,
    double? RelativeBrightness,
    double? UniformityRatio,
    bool StarTestInconclusive,
    bool BrightnessTestSkipped,
    bool CloudPresent,
    string Summary)
{
    /// <summary>Image positions of the expected stars, for the overlay.</summary>
    public IReadOnlyList<PixelPoint> Expected { get; init; } = Array.Empty<PixelPoint>();

    /// <summary>For each expected star, whether it was found.</summary>
    public IReadOnlyList<bool> Found { get; init; } = Array.Empty<bool>();

    /// <summary>True when neither test could run, so cloud is unknown rather than detected (REQ-50).</summary>
    public bool Undetermined => StarTestInconclusive && BrightnessTestSkipped;

    /// <summary>The Moon at the time of the frame.</summary>
    public MoonState? Moon { get; init; }
}
