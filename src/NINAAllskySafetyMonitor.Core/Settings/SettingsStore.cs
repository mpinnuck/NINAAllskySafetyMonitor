using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>
/// Loads and saves <see cref="MonitorSettings"/> as a JSON file. A missing file yields defaults.
/// Saving writes a unique temporary file first, preserves the previous file as a backup, and
/// then replaces the original so concurrent saves cannot share a partial file.
/// </summary>
[Requirement("REQ-55", "REQ-56", "REQ-57")]
public sealed class SettingsStore
{
    private readonly object sync = new();
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },

        // Keep characters such as + and ° readable in the file (the default escapes them as \uXXXX).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Creates a store for the settings file at <paramref name="path"/>.</summary>
    public SettingsStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Settings path is required.", nameof(path));
        Path = path;
    }

    /// <summary>Full path of the settings file.</summary>
    public string Path { get; }

    /// <summary>Path of the preserved invalid settings file after a successful recovery save.</summary>
    public string? LastCorruptBackupPath { get; private set; }

    /// <summary>Loads the settings, or returns defaults when the file does not exist.</summary>
    /// <exception cref="InvalidDataException">The file exists but is not valid settings JSON.</exception>
    public MonitorSettings Load()
    {
        if (!File.Exists(Path)) return new MonitorSettings();

        try
        {
            return SettingsMigrator.Migrate(FromJson(File.ReadAllText(Path)));
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Settings file '{Path}' is not valid: {e.Message}", e);
        }
    }

    /// <summary>Saves the settings, creating the folder if needed.</summary>
    public void Save(MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (sync)
        {
            LastCorruptBackupPath = null;
            string? folder = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            string temp = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                MonitorSettings safe = Clone(settings);
                safe.Weather.ApiKey = string.Empty;
                File.WriteAllText(temp, ToJson(safe));
                if (File.Exists(Path))
                {
                    string existing = File.ReadAllText(Path);
                    try
                    {
                        string backup = SafeBackupJson(existing);
                        File.WriteAllText(Path + ".bak", backup);
                    }
                    catch (InvalidDataException)
                    {
                        string recovery = Path + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture)
                            + "." + Guid.NewGuid().ToString("N");
                        File.Copy(Path, recovery);
                        try
                        {
                            File.Move(temp, Path, overwrite: true);
                        }
                        catch
                        {
                            File.Delete(recovery);
                            throw;
                        }

                        LastCorruptBackupPath = recovery;
                        return;
                    }
                }

                File.Move(temp, Path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    /// <summary>Serialises the settings exactly as <see cref="Save"/> writes them.</summary>
    public static string ToJson(MonitorSettings settings) => JsonSerializer.Serialize(settings, Options);

    /// <summary>Parses settings JSON; an empty document yields defaults.</summary>
    /// <exception cref="JsonException">The text is not valid settings JSON.</exception>
    public static MonitorSettings FromJson(string json)
    {
        MonitorSettings settings = JsonSerializer.Deserialize<MonitorSettings>(json, Options) ?? new MonitorSettings();
        return Normalize(settings);
    }

    /// <summary>Returns an independent deep copy, used by editors so unsaved changes never affect the running monitor.</summary>
    public static MonitorSettings Clone(MonitorSettings settings) => FromJson(ToJson(settings));

    private static MonitorSettings Normalize(MonitorSettings s)
    {
        s.Site ??= new SiteSettings();
        s.Window ??= new OperatingWindowSettings();
        s.ImageSource ??= new ImageSourceSettings();
        s.Roi ??= new RoiSettings();
        s.Mount ??= new MountSettings();
        s.LensFit ??= new LensFitSettings();
        s.Cloud ??= new CloudDetectionSettings();
        s.Weather ??= new WeatherSettings();
        s.Decision ??= new DecisionSettings();
        s.Tuning ??= new TuningSettings();
        s.Logging ??= new LoggingSettings();
        s.Calibration ??= new CalibrationData();

        s.ImageSource.Web ??= new WebSourceSettings();
        s.ImageSource.Direct ??= new DirectSourceSettings();
        s.Calibration.WorkingArea ??= new Geometry.WorkingAreaDefinition();
        s.Calibration.WorkingArea = s.Calibration.WorkingArea with
        {
            Outline = s.Calibration.WorkingArea.Outline ?? Array.Empty<PixelPoint>(),
        };
        if (s.Calibration.Lens is { RadialCoefficients: null } lens)
            s.Calibration.Lens = lens with { RadialCoefficients = Array.Empty<double>() };
        if (s.Calibration.ClearSkyReference is { Blocks: null })
            s.Calibration.ClearSkyReference = null;

        s.ImageSource.Web.BaseUrl ??= string.Empty;
        s.ImageSource.Web.FileName ??= string.Empty;
        s.ImageSource.Direct.Host ??= string.Empty;
        s.ImageSource.Direct.FallbackIp ??= string.Empty;
        s.ImageSource.Direct.User ??= string.Empty;
        s.ImageSource.Direct.RemotePath ??= string.Empty;
        s.ImageSource.Direct.KeyFile ??= string.Empty;
        s.ImageSource.Direct.HostKeyAlgorithm ??= string.Empty;
        s.ImageSource.Direct.HostKeyFingerprint ??= string.Empty;
        s.Weather.StationId ??= string.Empty;
        s.Weather.ApiKey ??= string.Empty;
        s.Logging.Folder ??= new LoggingSettings().Folder;
        s.Tuning.OutputFolder ??= new TuningSettings().OutputFolder;
        return s;
    }

    private static string SafeBackupJson(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("The existing settings file could not be backed up safely.", e);
        }

        if (root is JsonObject settings && Find(settings, "Weather") is JsonObject weather)
        {
            string? apiKey = weather.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, "ApiKey", StringComparison.OrdinalIgnoreCase));
            if (apiKey is not null) weather[apiKey] = string.Empty;
        }

        return root?.ToJsonString(Options) ?? "{}";
    }

    private static JsonNode? Find(JsonObject value, string name)
    {
        string? key = value.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        return key is null ? null : value[key];
    }
}
