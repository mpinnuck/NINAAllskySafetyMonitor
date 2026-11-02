using NINAAllskySafetyMonitor.Core.Geometry;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>A fitted lens model and how well it fits.</summary>
public sealed record LensFitResult(LensCalibration Lens, IReadOnlyList<StarMatch> Matches, double RmsErrorPx, double RmsErrorDeg)
{
    public int StarsMatched => Matches.Count;
}
