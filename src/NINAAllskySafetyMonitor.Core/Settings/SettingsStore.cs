using System.Text.Json;
using System.Text.Json.Serialization;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>
/// Loads and saves <see cref="MonitorSettings"/> as a JSON file. A missing file yields defaults.
/// Saving writes a temporary file first and then replaces the original, so a failed write
/// never leaves a half-written settings file.
/// </summary>
[Requirement("REQ-55", "REQ-56", "REQ-57")]
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Creates a store for the settings file at <paramref name="path"/>.</summary>
    public SettingsStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Settings path is required.", nameof(path));
        Path = path;
    }

    /// <summary>Full path of the settings file.</summary>
    public string Path { get; }

    /// <summary>Loads the settings, or returns defaults when the file does not exist.</summary>
    /// <exception cref="InvalidDataException">The file exists but is not valid settings JSON.</exception>
    public MonitorSettings Load()
    {
        if (!File.Exists(Path)) return new MonitorSettings();

        try
        {
            string json = File.ReadAllText(Path);
            return JsonSerializer.Deserialize<MonitorSettings>(json, Options) ?? new MonitorSettings();
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

        string? folder = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        string temp = Path + ".tmp";
        File.WriteAllText(temp, ToJson(settings));
        File.Move(temp, Path, overwrite: true);
    }

    /// <summary>Serialises the settings exactly as <see cref="Save"/> writes them.</summary>
    public static string ToJson(MonitorSettings settings) => JsonSerializer.Serialize(settings, Options);
}
