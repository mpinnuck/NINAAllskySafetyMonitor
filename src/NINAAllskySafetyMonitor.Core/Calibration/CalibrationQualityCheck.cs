using System.Globalization;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>Warns when a lens fit matched too few stars or its error is too large (REQ-20). Never refuses a fit.</summary>
[Requirement("REQ-20")]
public sealed class CalibrationQualityCheck
{
    private readonly Func<LensFitSettings> settings;

    public CalibrationQualityCheck(Func<LensFitSettings> settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public IReadOnlyList<string> Warnings(LensFitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LensFitSettings s = settings();
        var warnings = new List<string>();
        if (result.StarsMatched < s.MinMatchedStars)
        {
            warnings.Add($"Only {result.StarsMatched} stars matched (at least {s.MinMatchedStars} expected): check the frame is clear and its time is right.");
        }

        if (result.RmsErrorDeg > s.MaxFitErrorDeg)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Fit error {result.RmsErrorDeg:0.00}° is above the {s.MaxFitErrorDeg:0.00}° limit: check the overlay before saving."));
        }

        return warnings;
    }
}
