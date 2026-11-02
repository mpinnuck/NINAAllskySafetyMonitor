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
        MonitorSettings copy = SettingsStore.Clone(settings);
        store.Save(copy);
        lock (sync) current = copy;
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
            store.Save(s);
            current = s;
        }

        Saved?.Invoke(this, EventArgs.Empty);
    }

    private MonitorSettings LoadOrDefaults()
    {
        try
        {
            return store.Load();
        }
        catch (Exception e)
        {
            Logger.Error($"[AllskySafetyMonitor] Could not load settings, using defaults: {e.Message}");
            return new MonitorSettings();
        }
    }
}
