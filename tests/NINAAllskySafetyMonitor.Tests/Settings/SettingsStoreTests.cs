using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Settings;

[Requirement("REQ-55", "REQ-56", "REQ-57")]
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "NASM-Tests-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void MissingFileGivesDefaults()
    {
        MonitorSettings s = new SettingsStore(SettingsPath).Load();
        Assert.Equal(15.0, s.Roi.RadiusDeg);
        Assert.Equal(ImageSourceKind.Direct, s.ImageSource.Kind);
    }

    [Fact]
    public void SaveThenLoadRoundTripsValuesAndCalibration()
    {
        var store = new SettingsStore(SettingsPath);
        var s = new MonitorSettings();
        s.Roi.RadiusDeg = 12.5;
        s.ImageSource.Kind = ImageSourceKind.Web;
        s.Weather.MaxGustKmH = 35.0;
        s.Calibration.Lens = new LensCalibration
        {
            CentreX = 1520.5,
            CentreY = 1012.25,
            NorthRotationDeg = 87.0,
            EastIsClockwise = true,
            RadialCoefficients = new[] { 980.0, -12.5 },
            RmsErrorPx = 1.4,
            CalibratedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        s.Calibration.WorkingArea = new WorkingAreaDefinition
        {
            CentreAltDeg = 70,
            CentreAzDeg = 170,
            RadiusDeg = 55,
            Outline = new[] { new PixelPoint(10, 20), new PixelPoint(30, 40), new PixelPoint(50, 10) },
        };
        s.Roi.MinInsideFraction = 0.4;
        s.Calibration.ClearSkyReference = new ClearSkyReference { Median = 812.0, Uniformity = 0.06 };

        store.Save(s);
        MonitorSettings loaded = store.Load();

        Assert.Equal(12.5, loaded.Roi.RadiusDeg);
        Assert.Equal(ImageSourceKind.Web, loaded.ImageSource.Kind);
        Assert.Equal(35.0, loaded.Weather.MaxGustKmH);
        Assert.NotNull(loaded.Calibration.Lens);
        Assert.Equal(1520.5, loaded.Calibration.Lens!.CentreX);
        Assert.Equal(new[] { 980.0, -12.5 }, loaded.Calibration.Lens.RadialCoefficients);
        Assert.Equal((70.0, 170.0, 55.0), (loaded.Calibration.WorkingArea.CentreAltDeg, loaded.Calibration.WorkingArea.CentreAzDeg, loaded.Calibration.WorkingArea.RadiusDeg));
        Assert.Equal(3, loaded.Calibration.WorkingArea.Outline.Count);
        Assert.Equal(new PixelPoint(30, 40), loaded.Calibration.WorkingArea.Outline[1]);
        Assert.Equal(0.4, loaded.Roi.MinInsideFraction);
        Assert.Equal(812.0, loaded.Calibration.ClearSkyReference!.Median);
    }

    [Fact]
    public void FileIsJsonWithReadableEnums()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(new MonitorSettings());
        string json = File.ReadAllText(SettingsPath);

        Assert.StartsWith("{", json.TrimStart());
        Assert.Contains("\"Kind\": \"Direct\"", json);
    }

    [Fact]
    [Requirement("REQ-53")]
    public void SettingsFileNeverContainsAPassword()
    {
        var settings = new MonitorSettings();
        settings.ImageSource.Direct.UsePassword = true;
        string json = SettingsStore.ToJson(settings);

        // Any setting whose name mentions a password may only be a true/false flag (UsePassword),
        // never a value that could hold the password itself.
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var offending = new List<string>();
        FindPasswordValues(doc.RootElement, "$", offending);
        Assert.Empty(offending);
    }

    private static void FindPasswordValues(System.Text.Json.JsonElement element, string path, List<string> offending)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    string childPath = $"{path}.{property.Name}";
                    bool isFlag = property.Value.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False;
                    if (property.Name.Contains("password", StringComparison.OrdinalIgnoreCase) && !isFlag)
                    {
                        offending.Add(childPath);
                    }

                    FindPasswordValues(property.Value, childPath, offending);
                }

                break;
            case System.Text.Json.JsonValueKind.Array:
                int i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FindPasswordValues(item, $"{path}[{i++}]", offending);
                }

                break;
        }
    }

    [Fact]
    [Requirement("REQ-56")]
    public void FileKeepsPlusSignsReadable()
    {
        string json = SettingsStore.ToJson(new MonitorSettings());
        Assert.Contains("4+pcu7EpiK0Jrqg", json);
        Assert.DoesNotContain("\\u002B", json);
    }

    [Fact]
    public void InvalidJsonIsReported()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(SettingsPath, "{ not json");
        Assert.Throws<InvalidDataException>(() => new SettingsStore(SettingsPath).Load());
    }
}
