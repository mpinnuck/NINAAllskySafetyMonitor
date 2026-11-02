using System.Globalization;
using System.Reflection;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Stars brighter than magnitude 4.5 from the Yale Bright Star Catalogue (5th revised edition, public
/// domain), embedded in this assembly as Resources/BrightStars.csv: HR number, RA (hours) and Dec
/// (degrees) J2000, V magnitude, and a proper name for the brightest stars. Used by lens calibration and
/// cloud detection.
/// </summary>
[Requirement("REQ-20", "REQ-29")]
public sealed class StarCatalogue
{
    private const string ResourceName = "NINAAllskySafetyMonitor.Core.Resources.BrightStars.csv";
    private static readonly Lazy<StarCatalogue> Embedded = new(() => Parse(ReadResource()));

    public StarCatalogue(IReadOnlyList<CatalogueStar> stars)
    {
        Stars = stars ?? throw new ArgumentNullException(nameof(stars));
    }

    /// <summary>All stars, in catalogue order.</summary>
    public IReadOnlyList<CatalogueStar> Stars { get; }

    /// <summary>The embedded catalogue (read once).</summary>
    public static StarCatalogue LoadEmbedded() => Embedded.Value;

    /// <summary>Stars at or brighter than the given magnitude.</summary>
    public IEnumerable<CatalogueStar> BrighterThan(double limitMag) => Stars.Where(s => s.Magnitude <= limitMag);

    /// <summary>Stars with a proper name, brightest first.</summary>
    public IEnumerable<CatalogueStar> Named() => Stars.Where(s => !string.IsNullOrEmpty(s.Name)).OrderBy(s => s.Magnitude);

    /// <summary>Parses the CSV text: lines starting with '#' and the header are skipped.</summary>
    public static StarCatalogue Parse(string csv)
    {
        var stars = new List<CatalogueStar>();
        foreach (string raw in csv.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || !char.IsDigit(line[0])) continue;
            string[] f = line.Split(',');
            if (f.Length < 4) continue;
            stars.Add(new CatalogueStar(
                int.Parse(f[0], CultureInfo.InvariantCulture),
                double.Parse(f[1], CultureInfo.InvariantCulture),
                double.Parse(f[2], CultureInfo.InvariantCulture),
                double.Parse(f[3], CultureInfo.InvariantCulture),
                f.Length > 4 && f[4].Length > 0 ? f[4] : null));
        }

        return new StarCatalogue(stars);
    }

    private static string ReadResource()
    {
        using Stream stream = typeof(StarCatalogue).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded star catalogue {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
