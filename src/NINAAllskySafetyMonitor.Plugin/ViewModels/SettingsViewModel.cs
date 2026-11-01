using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Plugin.Nina;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// Settings editor for the plugin options page. Edits a copy of the saved settings; Save validates
/// and persists them, Revert discards unsaved edits. Slice 1 exposes the operating window, the
/// fallback site, the cycle interval and the weather station key; later slices add the rest.
/// </summary>
[Requirement("REQ-46", "REQ-55")]
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly PluginSettingsStore store;
    private readonly SettingsValidator validator = new();
    private MonitorSettings edit;
    private string message = string.Empty;

    public SettingsViewModel(PluginSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        edit = SettingsStore.Clone(store.Current);
        SaveCommand = new DelegateCommand(Save);
        RevertCommand = new DelegateCommand(Revert);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SaveCommand { get; }

    public ICommand RevertCommand { get; }

    /// <summary>Where the settings are stored.</summary>
    public string SettingsFilePath => store.FilePath;

    /// <summary>Result of the last Save, or the validation errors.</summary>
    public string Message
    {
        get => message;
        private set { message = value; OnPropertyChanged(); }
    }

    public double SunAltitudeLimitDeg
    {
        get => edit.Window.SunAltitudeLimitDeg;
        set { edit.Window.SunAltitudeLimitDeg = value; OnPropertyChanged(); }
    }

    public double FallbackLatitudeDeg
    {
        get => edit.Site.LatitudeDeg;
        set { edit.Site.LatitudeDeg = value; OnPropertyChanged(); }
    }

    public double FallbackLongitudeDeg
    {
        get => edit.Site.LongitudeDeg;
        set { edit.Site.LongitudeDeg = value; OnPropertyChanged(); }
    }

    public double FallbackElevationM
    {
        get => edit.Site.ElevationM;
        set { edit.Site.ElevationM = value; OnPropertyChanged(); }
    }

    public int IntervalS
    {
        get => edit.ImageSource.IntervalS;
        set { edit.ImageSource.IntervalS = value; OnPropertyChanged(); }
    }

    public bool WeatherEnabled
    {
        get => edit.Weather.Enabled;
        set { edit.Weather.Enabled = value; OnPropertyChanged(); }
    }

    public string WeatherStationId
    {
        get => edit.Weather.StationId;
        set { edit.Weather.StationId = value; OnPropertyChanged(); }
    }

    public string WeatherApiKey
    {
        get => edit.Weather.ApiKey;
        set { edit.Weather.ApiKey = value; OnPropertyChanged(); }
    }

    private void Save()
    {
        IReadOnlyList<string> errors = validator.Validate(edit);
        if (errors.Count > 0)
        {
            Message = "Not saved:\n" + string.Join("\n", errors);
            return;
        }

        try
        {
            store.Save(edit);
            Message = $"Saved {DateTime.Now:HH:mm:ss}. The monitor uses the new settings from its next cycle.";
        }
        catch (Exception e)
        {
            Message = $"Could not save settings: {e.Message}";
        }
    }

    private void Revert()
    {
        edit = SettingsStore.Clone(store.Current);
        Message = "Unsaved changes discarded.";
        OnPropertyChanged(string.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
