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
        s.Calibration.Mask = new HorizonMaskDefinition
        {
            MinAltitudeDeg = 25,
            Outline = new[] { new PixelPoint(10, 20), new PixelPoint(30, 40), new PixelPoint(50, 10) },
        };
        s.Calibration.ClearSkyReference = new ClearSkyReference { Median = 812.0, Uniformity = 0.06 };

        store.Save(s);
        MonitorSettings loaded = store.Load();

        Assert.Equal(12.5, loaded.Roi.RadiusDeg);
        Assert.Equal(ImageSourceKind.Web, loaded.ImageSource.Kind);
        Assert.Equal(35.0, loaded.Weather.MaxGustKmH);
        Assert.NotNull(loaded.Calibration.Lens);
        Assert.Equal(1520.5, loaded.Calibration.Lens!.CentreX);
        Assert.Equal(new[] { 980.0, -12.5 }, loaded.Calibration.Lens.RadialCoefficients);
        Assert.Equal(25.0, loaded.Calibration.Mask.MinAltitudeDeg);
        Assert.Equal(3, loaded.Calibration.Mask.Outline.Count);
        Assert.Equal(new PixelPoint(30, 40), loaded.Calibration.Mask.Outline[1]);
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
        string json = SettingsStore.ToJson(new MonitorSettings());
        Assert.DoesNotContain("password\":", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidJsonIsReported()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(SettingsPath, "{ not json");
        Assert.Throws<InvalidDataException>(() => new SettingsStore(SettingsPath).Load());
    }
}
