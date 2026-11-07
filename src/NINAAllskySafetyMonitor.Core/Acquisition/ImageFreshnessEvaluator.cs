using System.Globalization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>
/// The FreshImage condition: fails when there is no image, when the newest image is older than the
/// stale limit, or when no fetch has succeeded for the fetch-failure limit. Initial absence is
/// undetermined only until the fetch-failure limit elapses.
/// </summary>
[Requirement("REQ-15", "REQ-18", "REQ-50")]
public sealed class ImageFreshnessEvaluator
{
    /// <summary>Evaluates freshness at <paramref name="nowUtc"/>.</summary>
    public ConditionCheck Evaluate(SkyImage? latest, DateTime? lastSuccessUtc, string? lastError, DateTime nowUtc, ImageSourceSettings s,
        DateTime? firstAttemptUtc = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        string failure = lastError is null ? string.Empty : $": {lastError}";

        DateTime? lastAttemptOrigin = lastSuccessUtc ?? firstAttemptUtc;
        if (latest is null)
        {
            if (lastAttemptOrigin is DateTime first && nowUtc - first > TimeSpan.FromMinutes(s.FetchFailUnsafeMin))
            {
                double minutes = (nowUtc - first).TotalMinutes;
                return Failed($"No image fetched for {Format(minutes)} min (limit {s.FetchFailUnsafeMin} min){failure}");
            }

            return Fail(lastError is null ? "No image yet" : $"No image{failure}");
        }

        if (lastAttemptOrigin is null || nowUtc - lastAttemptOrigin.Value > TimeSpan.FromMinutes(s.FetchFailUnsafeMin))
        {
            double minutes = lastAttemptOrigin is null ? double.NaN : (nowUtc - lastAttemptOrigin.Value).TotalMinutes;
            return Failed($"No image fetched for {Format(minutes)} min (limit {s.FetchFailUnsafeMin} min){failure}");
        }

        // A Pi clock slightly ahead of the PC gives a small negative age; treat it as zero.
        double age = Math.Max(0, (nowUtc - latest.CaptureTimeUtc).TotalMinutes);
        if (age > s.StaleAfterMin)
        {
            return Failed($"Image is {Format(age)} min old (limit {s.StaleAfterMin} min)");
        }

        string note = lastError is null ? string.Empty : $" (last fetch failed{failure})";
        return new ConditionCheck(SafetyCondition.FreshImage, true, $"Image {Format(age)} min old{note}");
    }

    /// <summary>No image yet: the sky can't be judged, which is undetermined (REQ-50).</summary>
    private static ConditionCheck Fail(string reason) => ConditionCheck.Unknown(SafetyCondition.FreshImage, reason);

    private static ConditionCheck Failed(string reason) => new(SafetyCondition.FreshImage, false, reason);

    private static string Format(double minutes) =>
        double.IsNaN(minutes) ? "?" : minutes.ToString("0.0", CultureInfo.InvariantCulture);
}
