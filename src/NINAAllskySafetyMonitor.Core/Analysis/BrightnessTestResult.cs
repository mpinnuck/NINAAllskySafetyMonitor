using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>Outcome of the brightness test.</summary>
/// <param name="RelativeBrightness">ROI brightness relative to the rest of the working area, against the reference (1.0 = as clear sky).</param>
/// <param name="UniformityRatio">ROI non-uniformity ÷ reference.</param>
/// <param name="Skipped">True when there is no usable reference.</param>
/// <param name="Cloud">True when either ratio is above its limit.</param>
[Requirement("REQ-30")]
public sealed record BrightnessTestResult(double? RelativeBrightness, double? UniformityRatio, bool Skipped, bool Cloud);
