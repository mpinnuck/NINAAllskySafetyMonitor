using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>Outcome of the star-fraction test.</summary>
/// <param name="Expected">Stars expected.</param>
/// <param name="Matched">Expected stars found.</param>
/// <param name="Fraction">Matched ÷ Expected, or null when no star was expected.</param>
/// <param name="Inconclusive">True when too few expected stars are available for a decisive test.</param>
/// <param name="Cloud">True when the fraction or low-count probability test identifies cloud.</param>
[Requirement("REQ-29")]
public sealed record StarTestResult(int Expected, int Matched, double? Fraction, bool Inconclusive, bool Cloud);
