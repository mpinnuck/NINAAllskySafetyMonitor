using System.Globalization;
using System.Text.RegularExpressions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>
/// When a calibration frame was taken. Allsky's saved frames carry the local time in their name
/// (allsky-20261020022133.jpg); the live image's capture time is recorded when it is fetched.
/// </summary>
[Requirement("REQ-20")]
public sealed class FrameTimeReader
{
    private static readonly Regex Stamp = new(@"(\d{14})(?!.*\d{14})", RegexOptions.Compiled);

    /// <summary>
    /// The UTC time from the last 14-digit yyyyMMddHHmmss group in the file name, read as local time in
    /// <paramref name="zone"/> (default: this computer's zone). Null when the name has no valid time.
    /// </summary>
    public DateTime? FromFileName(string fileName, TimeZoneInfo? zone = null)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        Match m = Stamp.Match(Path.GetFileNameWithoutExtension(fileName));
        if (!m.Success) return null;
        if (!DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local))
        {
            return null;
        }

        TimeZoneInfo tz = zone ?? TimeZoneInfo.Local;
        if (tz.IsInvalidTime(local)) return null;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);
    }

    /// <summary>The live image's capture time (file modification or web Last-Modified time).</summary>
    public DateTime FromSkyImage(SkyImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return image.CaptureTimeUtc;
    }
}
