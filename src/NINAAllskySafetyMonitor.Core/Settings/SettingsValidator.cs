using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Checks settings for values that would stop the monitor working. Returns readable error messages.</summary>
[Requirement("REQ-22", "REQ-27", "REQ-46", "REQ-58")]
public sealed class SettingsValidator
{
    /// <summary>Returns one message per problem; an empty list means the settings are valid.</summary>
    public IReadOnlyList<string> Validate(MonitorSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var errors = new List<string>();

        Range(errors, s.Site.LatitudeDeg, -90, 90, "Site latitude must be between −90° and 90°.");
        Range(errors, s.Site.LongitudeDeg, -180, 180, "Site longitude must be between −180° and 180°.");
        Range(errors, s.Window.SunAltitudeLimitDeg, -30, 90, "Sun altitude limit must be between −30° and +90° (+90 keeps the monitor active in daylight, for testing).");

        ImageSourceSettings img = s.ImageSource;
        Range(errors, img.IntervalS, 10, 3600, "Image interval must be between 10 s and 3600 s.");
        Range(errors, img.StaleAfterMin, 1, 120, "Stale-image limit must be between 1 and 120 minutes.");
        Range(errors, img.FetchFailUnsafeMin, 1, 120, "Fetch-failure limit must be between 1 and 120 minutes.");
        Range(errors, img.IncompleteRetryS, 1, 60, "Incomplete-image retry delay must be between 1 s and 60 s.");
        Range(errors, img.IncompleteRetries, 1, 10, "Incomplete-image attempts must be between 1 and 10.");
        Range(errors, img.TimeoutS, 1, 300, "Image fetch timeout must be between 1 s and 300 s.");

        if (img.Kind == ImageSourceKind.Web &&
            !Uri.TryCreate(img.Web.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("Web source base URL must be an absolute URL.");
        }

        if (img.Kind == ImageSourceKind.Web && string.IsNullOrWhiteSpace(img.Web.FileName))
        {
            errors.Add("Web source image file name is required.");
        }

        if (img.Kind == ImageSourceKind.Direct)
        {
            if (string.IsNullOrWhiteSpace(img.Direct.Host)) errors.Add("Allsky Pi host is required.");
            if (string.IsNullOrWhiteSpace(img.Direct.User)) errors.Add("Allsky Pi user is required.");
            if (string.IsNullOrWhiteSpace(img.Direct.RemotePath)) errors.Add("Allsky image path on the Pi is required.");
            if (!img.Direct.UsePassword && string.IsNullOrWhiteSpace(img.Direct.KeyFile))
            {
                errors.Add("An SSH key file is required unless password authentication is selected.");
            }

            if (string.IsNullOrWhiteSpace(img.Direct.HostKeyFingerprint))
            {
                errors.Add("The Pi's host-key fingerprint is required.");
            }

            if (string.IsNullOrWhiteSpace(img.Direct.HostKeyAlgorithm))
            {
                errors.Add("The host-key type (for example ecdsa-sha2-nistp256 or ssh-ed25519) is required.");
            }

            Range(errors, img.Direct.Port, 1, 65535, "SFTP port must be between 1 and 65535.");
        }

        Range(errors, s.Roi.RadiusDeg, 1, 90, "ROI radius must be between 1° and 90°.");
        Range(errors, s.Mount.PollIntervalS, 1, 60, "Mount check interval must be between 1 s and 60 s.");
        Range(errors, s.Roi.MinInsideFraction, 0, 1, "Minimum ROI fraction inside the working area must be between 0% and 100%.");

        Geometry.WorkingAreaDefinition area = s.Calibration.WorkingArea;
        Range(errors, area.CentreAltDeg, 0, 90, "Working area centre altitude must be between 0° and 90°.");
        Range(errors, area.CentreAzDeg, 0, 360, "Working area centre azimuth must be between 0° and 360°.");
        Range(errors, area.RadiusDeg, 5, 90, "Working area radius must be between 5° and 90°.");

        if (s.Calibration.Lens is { } lens)
        {
            if (lens.RadialCoefficients.Length == 0 || !(lens.RadialCoefficients[0] > 0))
            {
                errors.Add("The lens model needs a positive horizon radius.");
            }

            Range(errors, lens.NorthRotationDeg, -360, 360, "Lens north rotation must be between −360° and 360°.");
        }

        CloudDetectionSettings c = s.Cloud;
        Range(errors, c.LimitingMagnitude, 0, 7, "Limiting magnitude must be between 0 and 7.");
        Range(errors, c.MinStarFraction, 0, 1, "Minimum star fraction must be between 0 and 1.");
        Range(errors, c.MinExpectedStars, 1, 500, "Minimum expected stars must be between 1 and 500.");
        Range(errors, c.MaxBrightnessRatio, 1, 10, "Maximum brightness ratio must be between 1 and 10.");
        Range(errors, c.MaxUniformityRatio, 1, 10, "Maximum uniformity ratio must be between 1 and 10.");
        Range(errors, c.MoonExclusionDeg, 0, 90, "Moon exclusion must be between 0° and 90°.");

        WeatherSettings w = s.Weather;
        if (w.Enabled)
        {
            if (string.IsNullOrWhiteSpace(w.StationId)) errors.Add("Weather station ID is required.");
            if (string.IsNullOrWhiteSpace(w.ApiKey)) errors.Add("Weather API key is required, or switch weather checking off.");
            Range(errors, w.PollIntervalS, 30, 3600, "Weather poll interval must be between 30 s and 3600 s.");
            Range(errors, w.MaxGustKmH, 0, 200, "Maximum wind gust must be between 0 and 200 km/h.");
            Range(errors, w.StaleAfterMin, 1, 120, "Weather data age limit must be between 1 and 120 minutes.");
            Range(errors, w.TimeoutS, 1, 120, "Weather request timeout must be between 1 s and 120 s.");
        }

        Range(errors, s.Decision.SafeAfterMin, 0, 120, "Safe-after period must be between 0 and 120 minutes.");
        Range(errors, s.Logging.RetentionDays, 1, 365, "Log retention must be between 1 and 365 days.");

        return errors;
    }

    private static void Range(List<string> errors, double value, double min, double max, string message)
    {
        if (double.IsNaN(value) || value < min || value > max) errors.Add(message);
    }
}
