using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// Picks the results worth checking by eye: a target whose verdict differs from its previous frame in the
/// same night, or a metric within NearLimit [10%] of its limit (star fraction, relative brightness,
/// non-uniformity ratio).
/// </summary>
[Requirement("REQ-61")]
public sealed class ReviewSelector
{
    private readonly double nearLimit;
    private readonly Dictionary<string, (DateOnly Night, Verdict Verdict)> previous = new(StringComparer.Ordinal);

    public ReviewSelector(double nearLimit)
    {
        this.nearLimit = nearLimit;
    }

    public IReadOnlyList<(TargetResult Result, string Why)> Select(FrameResult frame, CloudDetectionSettings limits)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(limits);
        var picked = new List<(TargetResult, string)>();
        foreach (TargetResult t in frame.Targets)
        {
            var why = new List<string>();
            if (previous.TryGetValue(t.Target.Id, out var p) && p.Night == frame.Frame.Night && p.Verdict != t.Verdict)
            {
                why.Add($"{p.Verdict} → {t.Verdict}");
            }

            previous[t.Target.Id] = (frame.Frame.Night, t.Verdict);
            if (!t.Cloud.StarTestInconclusive && Near(t.Cloud.StarFraction, limits.MinStarFraction)) why.Add("star fraction near limit");
            if (Near(t.Cloud.RelativeBrightness, limits.MaxBrightnessRatio)) why.Add("brightness near limit");
            if (Near(t.Cloud.UniformityRatio, limits.MaxUniformityRatio)) why.Add("non-uniformity near limit");
            if (why.Count > 0) picked.Add((t, string.Join("; ", why)));
        }

        return picked;
    }

    private bool Near(double? value, double limit) => value is double v && Math.Abs(v - limit) <= nearLimit * limit;
}
