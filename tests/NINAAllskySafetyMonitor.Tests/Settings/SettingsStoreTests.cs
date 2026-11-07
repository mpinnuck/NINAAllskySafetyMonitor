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
        var blocks = new BlockStatistics(32, 64, 32, new[] { 81.25f, float.NaN }, new[] { 0.06f, float.NaN });
        s.Calibration.ClearSkyReference = new ClearSkyReference(blocks, new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));

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
        BlockStatistics map = loaded.Calibration.ClearSkyReference!.Blocks;
        Assert.Equal(81.25f, map.Median[0]);
        Assert.True(map.IsValid(0));
        Assert.False(map.IsValid(1));
        Assert.Equal(0.06f, map.Uniformity[0], 4);
    }

    [Fact]
    public void SaveKeepsThePreviousFileAsBackup()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(new MonitorSettings { SchemaVersion = 2 });
        store.Save(new MonitorSettings { SchemaVersion = 3 });

        MonitorSettings backup = SettingsStore.FromJson(File.ReadAllText(SettingsPath + ".bak"));
        Assert.Equal(2, backup.SchemaVersion);
        Assert.Equal(3, store.Load().SchemaVersion);
    }

    [Fact]
    public void ConcurrentSavesDoNotCorruptSettingsOrShareTemporaryFiles()
    {
        var store = new SettingsStore(SettingsPath);
        Parallel.For(0, 20, version => store.Save(new MonitorSettings { SchemaVersion = version }));

        Assert.InRange(store.Load().SchemaVersion, 0, 19);
        Assert.Empty(Directory.GetFiles(folder, "settings.json.*.tmp"));
        Assert.True(File.Exists(SettingsPath + ".bak"));
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
    [Requirement("REQ-30", "REQ-57")]
    public void ClearSkyMapIsWrittenCompactly()
    {
        var s = new MonitorSettings();
        s.Calibration.ClearSkyReference = new ClearSkyReference(
            new BlockStatistics(32, 96, 32, new[] { 20.5f, 21f, float.NaN }, new[] { 0.05f, 0.0625f, float.NaN }), DateTime.UnixEpoch);
        string json = SettingsStore.ToJson(s);
        Assert.Contains("\"20.5 21 -\"", json);
        Assert.Contains("\"0.05 0.0625 -\"", json);
    }

    [Fact]
    [Requirement("REQ-29", "REQ-55")]
    public void OldDefaultLimitingMagnitudeIsMigrated()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(SettingsPath, "{ \"SchemaVersion\": 1, \"Cloud\": { \"LimitingMagnitude\": 4.0 } }");
        Assert.Equal(4.4, new SettingsStore(SettingsPath).Load().Cloud.LimitingMagnitude);

        File.WriteAllText(SettingsPath, "{ \"SchemaVersion\": 1, \"Cloud\": { \"LimitingMagnitude\": 3.5 } }");
        MonitorSettings kept = new SettingsStore(SettingsPath).Load();
        Assert.Equal(3.5, kept.Cloud.LimitingMagnitude);
        Assert.Equal(SettingsMigrator.CurrentVersion, kept.SchemaVersion);
    }

    [Fact]
    public void InvalidJsonIsReported()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(SettingsPath, "{ not json");
        Assert.Throws<InvalidDataException>(() => new SettingsStore(SettingsPath).Load());
    }
}
