using System.ComponentModel;
using System.Globalization;
using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// The cloud metrics behind the latest decision, as lines of text for the status panel: the star test,
/// the brightness test with their limits, the Moon, and the clear-sky reference.
/// </summary>
[Requirement("REQ-45")]
public sealed class MetricsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string StarsText { get; private set; } = string.Empty;

    public string BrightnessText { get; private set; } = string.Empty;

    public string MoonText { get; private set; } = string.Empty;

    public string ReferenceText { get; private set; } = string.Empty;

    public bool HasMetrics => StarsText.Length > 0;

    /// <summary>Updates the lines from the latest snapshot (null when not connected) and the settings.</summary>
    public void Update(MonitorSnapshot? snapshot, MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        CloudAssessment? c = snapshot?.Cloud;
        CloudDetectionSettings s = settings.Cloud;
        StarsText = Stars(c, s);
        BrightnessText = Brightness(c, s);
        MoonText = Moon(c?.Moon);
        ReferenceText = Reference(snapshot is null ? null : settings.Calibration.ClearSkyReference, s.Tuned);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    internal static string Stars(CloudAssessment? c, CloudDetectionSettings s)
    {
        if (c is null) return string.Empty;
        if (c.StarTestInconclusive)
        {
            return string.Create(CultureInfo.CurrentCulture,
                $"Stars: {c.DetectedStars} of {c.ExpectedStars} found · too few expected for the star test (needs {s.MinExpectedStars}, mag ≤ {s.LimitingMagnitude:0.0})");
        }

        return string.Create(CultureInfo.CurrentCulture,
            $"Stars: {c.DetectedStars} of {c.ExpectedStars} found ({c.StarFraction:0%}) · cloud below {s.MinStarFraction:0%} · mag ≤ {s.LimitingMagnitude:0.0}");
    }

    internal static string Brightness(CloudAssessment? c, CloudDetectionSettings s)
    {
        if (c is null) return string.Empty;
        if (c.BrightnessTestSkipped) return "Brightness: no clear-sky reference yet, test skipped";
        string uniformity = c.UniformityRatio is double u
            ? string.Create(CultureInfo.CurrentCulture, $" · non-uniformity {u:0.00}× (limit {s.MaxUniformityRatio:0.00}×)")
            : string.Empty;
        return string.Create(CultureInfo.CurrentCulture,
            $"Brightness {c.RelativeBrightness:0.00}× clear sky (limit {s.MaxBrightnessRatio:0.00}×){uniformity}");
    }

    internal static string Moon(MoonState? m)
    {
        if (m is null) return string.Empty;
        return m.IsUp
            ? string.Create(CultureInfo.CurrentCulture, $"Moon up {m.Position.AltDeg:0}°, {m.Illumination:0%} lit: stars near it are not expected, brightness limits raised")
            : string.Create(CultureInfo.CurrentCulture, $"Moon down ({m.Illumination:0%} lit)");
    }

    internal static string Reference(ClearSkyReference? r, bool tuned)
    {
        if (r is not null)
        {
            return string.Create(CultureInfo.CurrentCulture, $"Clear-sky reference: {r.CapturedUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        }

        return tuned
            ? "Clear-sky reference: none yet, captured on the next clear, moonless frame"
            : "Clear-sky reference: none yet, captured on the first clear, moonless night after the thresholds are marked tuned";
    }
}
