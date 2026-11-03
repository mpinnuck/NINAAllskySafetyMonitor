using System.Globalization;
using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// Lists saved frames in time order. Allsky keeps one folder per night, named after the date the night
/// started (yyyyMMdd), holding frames named …-yyyyMMddHHmmss.jpg with the local capture time. Sub-folders
/// (thumbnails, keograms, startrails, videos) are ignored. The folder given may be the images folder or a
/// single night folder. Frames closer than StepS to the previous one used are skipped.
/// </summary>
[Requirement("REQ-42")]
public sealed class FrameCatalog
{
    private readonly FrameTimeReader times = new();

    public IReadOnlyList<SavedFrame> List(ReplayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Directory.Exists(options.FramesFolder)) throw new DirectoryNotFoundException($"Frames folder not found: {options.FramesFolder}");

        var nights = new List<(DateOnly Night, string Folder)>();
        if (TryNight(Path.GetFileName(Path.TrimEndingDirectorySeparator(options.FramesFolder)), out DateOnly self))
        {
            nights.Add((self, options.FramesFolder));
        }
        else
        {
            foreach (string dir in Directory.EnumerateDirectories(options.FramesFolder))
            {
                if (TryNight(Path.GetFileName(dir), out DateOnly n)) nights.Add((n, dir));
            }
        }

        var frames = new List<SavedFrame>();
        foreach ((DateOnly night, string folder) in nights.OrderBy(n => n.Night))
        {
            if (options.FirstNight is DateOnly first && night < first) continue;
            if (options.LastNight is DateOnly last && night > last) continue;
            foreach (string file in Directory.EnumerateFiles(folder, "*.jpg", SearchOption.TopDirectoryOnly))
            {
                if (times.FromFileName(file, options.Zone) is DateTime utc) frames.Add(new SavedFrame(file, night, utc));
            }
        }

        frames.Sort((a, b) => a.CaptureUtc.CompareTo(b.CaptureUtc));
        return Thin(frames, options.StepS);
    }

    internal static IReadOnlyList<SavedFrame> Thin(IReadOnlyList<SavedFrame> frames, int stepS)
    {
        var kept = new List<SavedFrame>();
        DateTime? last = null;
        foreach (SavedFrame f in frames)
        {
            if (last is DateTime l && (f.CaptureUtc - l).TotalSeconds < stepS - 1) continue;
            kept.Add(f);
            last = f.CaptureUtc;
        }

        return kept;
    }

    internal static bool TryNight(string name, out DateOnly night) =>
        DateOnly.TryParseExact(name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out night);
}
