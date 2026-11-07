using System.IO;
using NINA.Core.Utility;
using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>
/// Holds the plugin's settings, persisted as JSON in
/// %LOCALAPPDATA%\NINAAllskySafetyMonitor\settings.json. A single shared instance is used
/// because NINA creates the plugin, the device and the dock panel independently.
/// </summary>
[Requirement("REQ-30", "REQ-55", "REQ-56", "REQ-57")]
public sealed class PluginSettingsStore
{
    private readonly SettingsStore store;
    private readonly object sync = new();
    private MonitorSettings current;
    private string? loadError;

    private PluginSettingsStore()
    {
        string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINAAllskySafetyMonitor");
        store = new SettingsStore(System.IO.Path.Combine(folder, "settings.json"));
        current = LoadOrDefaults();
    }

    /// <summary>The shared instance.</summary>
    public static PluginSettingsStore Instance { get; } = new();

    /// <summary>Full path of the settings file.</summary>
    public string FilePath => store.Path;

    /// <summary>Load error shown in the options page; the original file is left intact for recovery.</summary>
    public string? LoadError => loadError;

    /// <summary>The saved settings in use by the monitor. Treat as read-only; edit a clone and call <see cref="Save"/>.</summary>
    public MonitorSettings Current
    {
        get { lock (sync) return current; }
    }

    /// <summary>Raised after settings are saved.</summary>
    public event EventHandler? Saved;

    /// <summary>Saves the settings and makes them current. The running monitor uses them from its next cycle.</summary>
    public void Save(MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (sync)
        {
            MonitorSettings copy = SettingsStore.Clone(settings);
            copy.Weather.ApiKey = string.Empty;
            SaveLocked(copy);
            current = copy;
        }

        Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stores the clear-sky reference (null clears it) in the saved settings, leaving everything else as
    /// saved. Used by the monitor when it captures a reference, and by the Options page to clear it.
    /// </summary>
    public void SaveReference(ClearSkyReference? reference)
    {
        lock (sync)
        {
            MonitorSettings s = SettingsStore.Clone(current);
            s.Calibration.ClearSkyReference = reference;
            s.Weather.ApiKey = string.Empty;
            SaveLocked(s);
            current = s;
        }

        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void SaveLocked(MonitorSettings next)
    {
        MonitorSettings? previous = string.IsNullOrEmpty(current.Weather.ApiKey)
            ? null
            : SettingsStore.Clone(current);
        store.Save(next);
        if (previous is not null)
        {
            previous.Weather.ApiKey = string.Empty;
            File.WriteAllText(store.Path + ".bak", SettingsStore.ToJson(previous));
        }
    }

    private MonitorSettings LoadOrDefaults()
    {
        try
        {
            MonitorSettings loaded = store.Load();
            loadError = null;
            return loaded;
        }
        catch (Exception e)
        {
            loadError = $"Settings could not be loaded; defaults are in use. The original file was not changed. {e.Message}";
            Logger.Error($"[AllskySafetyMonitor] Could not load settings, using defaults: {e.Message}");
            return new MonitorSettings();
        }
    }
}
