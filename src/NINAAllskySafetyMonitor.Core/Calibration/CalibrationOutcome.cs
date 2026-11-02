namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>Result of a calibration attempt.</summary>
/// <param name="Result">The fit, or null when none was found.</param>
/// <param name="Warnings">Quality warnings (REQ-20); empty when the fit is good.</param>
/// <param name="NeedsIdentification">True when the automatic search failed: the imager should identify 2–3 stars.</param>
/// <param name="DetectedStars">How many star-like spots were found in the frame.</param>
/// <param name="Message">One line for the user.</param>
public sealed record CalibrationOutcome(LensFitResult? Result, IReadOnlyList<string> Warnings, bool NeedsIdentification, int DetectedStars, string Message);
