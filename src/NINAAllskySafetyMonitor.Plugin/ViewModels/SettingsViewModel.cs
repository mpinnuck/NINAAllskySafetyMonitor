using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;
using NINAAllskySafetyMonitor.Plugin.Nina;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// Settings editor for the plugin options page. Edits a copy of the saved settings; Save validates
/// and persists them, Revert discards unsaved edits. The SFTP password goes to the credential store,
/// never into the settings. Each slice adds the settings it uses.
/// </summary>
[Requirement("REQ-22", "REQ-23", "REQ-46", "REQ-53", "REQ-55", "REQ-58")]
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly PluginSettingsStore store;
    private readonly ICredentialStore credentials;
    private readonly ImageFetchTester tester;
    private readonly WeatherFetchTester weatherTester = new(new SystemClock());
    private readonly SettingsValidator validator = new();
    private MonitorSettings edit;
    private string? newPassword;
    private string message = string.Empty;
    private bool testing;

    public SettingsViewModel(PluginSettingsStore store, ICredentialStore credentials)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        tester = new ImageFetchTester(credentials, new NinaLogSink(), new SystemClock());
        edit = SettingsStore.Clone(store.Current);
        SaveCommand = new DelegateCommand(Save);
        RevertCommand = new DelegateCommand(Revert);
        TestImageCommand = new DelegateCommand(TestImage);
        TestWeatherCommand = new DelegateCommand(TestWeather);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SaveCommand { get; }

    public ICommand RevertCommand { get; }

    public ICommand TestImageCommand { get; }

    public ICommand TestWeatherCommand { get; }

    /// <summary>Where the settings are stored.</summary>
    public string SettingsFilePath => store.FilePath;

    /// <summary>Result of the last Save or test, or the validation errors.</summary>
    public string Message
    {
        get => message;
        private set { message = value; OnPropertyChanged(); }
    }

    // ---- Operating window and cycle ----

    public double SunAltitudeLimitDeg
    {
        get => edit.Window.SunAltitudeLimitDeg;
        set { edit.Window.SunAltitudeLimitDeg = value; OnPropertyChanged(); }
    }

    public int IntervalS
    {
        get => edit.ImageSource.IntervalS;
        set { edit.ImageSource.IntervalS = value; OnPropertyChanged(); }
    }

    public int MountPollIntervalS
    {
        get => edit.Mount.PollIntervalS;
        set { edit.Mount.PollIntervalS = value; OnPropertyChanged(); }
    }

    // ---- Image source ----

    public IReadOnlyList<ImageSourceKind> ImageSourceKinds { get; } = new[] { ImageSourceKind.Direct, ImageSourceKind.Web };

    public ImageSourceKind ImageSourceKind
    {
        get => edit.ImageSource.Kind;
        set { edit.ImageSource.Kind = value; OnPropertyChanged(); }
    }

    public int ImageTimeoutS
    {
        get => edit.ImageSource.TimeoutS;
        set { edit.ImageSource.TimeoutS = value; OnPropertyChanged(); }
    }

    public int StaleAfterMin
    {
        get => edit.ImageSource.StaleAfterMin;
        set { edit.ImageSource.StaleAfterMin = value; OnPropertyChanged(); }
    }

    public int FetchFailUnsafeMin
    {
        get => edit.ImageSource.FetchFailUnsafeMin;
        set { edit.ImageSource.FetchFailUnsafeMin = value; OnPropertyChanged(); }
    }

    public int IncompleteRetryS
    {
        get => edit.ImageSource.IncompleteRetryS;
        set { edit.ImageSource.IncompleteRetryS = value; OnPropertyChanged(); }
    }

    public int IncompleteRetries
    {
        get => edit.ImageSource.IncompleteRetries;
        set { edit.ImageSource.IncompleteRetries = value; OnPropertyChanged(); }
    }

    public string DirectHost
    {
        get => edit.ImageSource.Direct.Host;
        set { edit.ImageSource.Direct.Host = value; OnPropertyChanged(); }
    }

    public string DirectFallbackIp
    {
        get => edit.ImageSource.Direct.FallbackIp;
        set { edit.ImageSource.Direct.FallbackIp = value; OnPropertyChanged(); }
    }

    public int DirectPort
    {
        get => edit.ImageSource.Direct.Port;
        set { edit.ImageSource.Direct.Port = value; OnPropertyChanged(); }
    }

    public string DirectUser
    {
        get => edit.ImageSource.Direct.User;
        set { edit.ImageSource.Direct.User = value; OnPropertyChanged(); }
    }

    public string DirectRemotePath
    {
        get => edit.ImageSource.Direct.RemotePath;
        set { edit.ImageSource.Direct.RemotePath = value; OnPropertyChanged(); }
    }

    public string DirectKeyFile
    {
        get => edit.ImageSource.Direct.KeyFile;
        set { edit.ImageSource.Direct.KeyFile = value; OnPropertyChanged(); }
    }

    public bool DirectUsePassword
    {
        get => edit.ImageSource.Direct.UsePassword;
        set { edit.ImageSource.Direct.UsePassword = value; OnPropertyChanged(); }
    }

    public string DirectHostKeyAlgorithm
    {
        get => edit.ImageSource.Direct.HostKeyAlgorithm;
        set { edit.ImageSource.Direct.HostKeyAlgorithm = value; OnPropertyChanged(); }
    }

    public string DirectHostKeyFingerprint
    {
        get => edit.ImageSource.Direct.HostKeyFingerprint;
        set { edit.ImageSource.Direct.HostKeyFingerprint = value; OnPropertyChanged(); }
    }

    /// <summary>A newly typed SFTP password; saved to the credential store on Save, never to the settings file.</summary>
    public string SftpPassword
    {
        get => newPassword ?? string.Empty;
        set { newPassword = value; OnPropertyChanged(); }
    }

    public string PasswordStatus
    {
        get
        {
            try
            {
                return string.IsNullOrEmpty(credentials.GetSecret(CredentialKeys.SftpPassword))
                    ? "No password stored."
                    : "A password is stored in Windows Credential Manager. Type a new one to replace it.";
            }
            catch (Exception e)
            {
                return $"Credential Manager unavailable: {e.Message}";
            }
        }
    }

    public string WebBaseUrl
    {
        get => edit.ImageSource.Web.BaseUrl;
        set { edit.ImageSource.Web.BaseUrl = value; OnPropertyChanged(); }
    }

    public string WebFileName
    {
        get => edit.ImageSource.Web.FileName;
        set { edit.ImageSource.Web.FileName = value; OnPropertyChanged(); }
    }

    // ---- Decision and log ----

    public int SafeAfterMin
    {
        get => edit.Decision.SafeAfterMin;
        set { edit.Decision.SafeAfterMin = value; OnPropertyChanged(); }
    }

    public int LogRetentionDays
    {
        get => edit.Logging.RetentionDays;
        set { edit.Logging.RetentionDays = value; OnPropertyChanged(); }
    }

    public string LogFolder
    {
        get => edit.Logging.Folder;
        set { edit.Logging.Folder = value; OnPropertyChanged(); }
    }

    // ---- Working area and ROI ----

    public double WorkingAreaCentreAltDeg
    {
        get => edit.Calibration.WorkingArea.CentreAltDeg;
        set { edit.Calibration.WorkingArea = edit.Calibration.WorkingArea with { CentreAltDeg = value }; OnPropertyChanged(); }
    }

    public double WorkingAreaCentreAzDeg
    {
        get => edit.Calibration.WorkingArea.CentreAzDeg;
        set { edit.Calibration.WorkingArea = edit.Calibration.WorkingArea with { CentreAzDeg = value }; OnPropertyChanged(); }
    }

    public double WorkingAreaRadiusDeg
    {
        get => edit.Calibration.WorkingArea.RadiusDeg;
        set { edit.Calibration.WorkingArea = edit.Calibration.WorkingArea with { RadiusDeg = value }; OnPropertyChanged(); }
    }

    public double RoiRadiusDeg
    {
        get => edit.Roi.RadiusDeg;
        set { edit.Roi.RadiusDeg = value; OnPropertyChanged(); }
    }

    /// <summary>Minimum ROI fraction inside the working area, shown as a percentage.</summary>
    public double RoiMinInsidePercent
    {
        get => Math.Round(edit.Roi.MinInsideFraction * 100, 1);
        set { edit.Roi.MinInsideFraction = value / 100.0; OnPropertyChanged(); }
    }

    // ---- Lens model, entered by hand until calibrated (slice 5b) ----

    /// <summary>On: use the values below. Off: estimate the model from the image size.</summary>
    public bool ManualLensEnabled
    {
        get => edit.Calibration.Lens is not null;
        set
        {
            if (value == ManualLensEnabled) return;
            edit.Calibration.Lens = value ? StartingLens() : null;
            OnPropertyChanged(string.Empty);
        }
    }

    public double LensCentreX
    {
        get => edit.Calibration.Lens?.CentreX ?? 0;
        set => SetLens(value, LensCentreY, LensHorizonRadiusPx, LensNorthRotationDeg, LensEastClockwise);
    }

    public double LensCentreY
    {
        get => edit.Calibration.Lens?.CentreY ?? 0;
        set => SetLens(LensCentreX, value, LensHorizonRadiusPx, LensNorthRotationDeg, LensEastClockwise);
    }

    public double LensHorizonRadiusPx
    {
        get => edit.Calibration.Lens is { RadialCoefficients: [> 0, ..] } lens ? Math.Round(new LensProjection(lens).HorizonRadiusPx, 1) : 0;
        set => SetLens(LensCentreX, LensCentreY, value, LensNorthRotationDeg, LensEastClockwise);
    }

    public double LensNorthRotationDeg
    {
        get => edit.Calibration.Lens?.NorthRotationDeg ?? 0;
        set => SetLens(LensCentreX, LensCentreY, LensHorizonRadiusPx, value, LensEastClockwise);
    }

    public bool LensEastClockwise
    {
        get => edit.Calibration.Lens?.EastIsClockwise ?? false;
        set => SetLens(LensCentreX, LensCentreY, LensHorizonRadiusPx, LensNorthRotationDeg, value);
    }

    private void SetLens(double x, double y, double radius, double rotation, bool clockwise)
    {
        if (edit.Calibration.Lens is null) return;
        edit.Calibration.Lens = LensProjection.FromHorizonRadius(x, y, radius, rotation, clockwise) with { CalibratedUtc = DateTime.UtcNow };
        OnPropertyChanged(nameof(LensCentreX));
        OnPropertyChanged(nameof(LensCentreY));
        OnPropertyChanged(nameof(LensHorizonRadiusPx));
        OnPropertyChanged(nameof(LensNorthRotationDeg));
        OnPropertyChanged(nameof(LensEastClockwise));
    }

    /// <summary>Starting values for a hand-entered model: the estimate for the latest image, if there is one.</summary>
    private static LensCalibration StartingLens()
    {
        try
        {
            byte[]? jpeg = MonitorHost.Instance.LatestSnapshot?.Image?.JpegBytes;
            if (jpeg is not null)
            {
                using var stream = new System.IO.MemoryStream(jpeg);
                var frame = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    stream, System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation, System.Windows.Media.Imaging.BitmapCacheOption.None).Frames[0];
                return LensProjection.Approximate(frame.PixelWidth, frame.PixelHeight) with { CalibratedUtc = DateTime.UtcNow };
            }
        }
        catch (Exception)
        {
            // Fall through to generic values.
        }

        return LensProjection.FromHorizonRadius(1500, 1000, 1000, 0, false) with { CalibratedUtc = DateTime.UtcNow };
    }

    // ---- Fallback site ----

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

    // ---- Weather ----

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

    public int WeatherPollIntervalS
    {
        get => edit.Weather.PollIntervalS;
        set { edit.Weather.PollIntervalS = value; OnPropertyChanged(); }
    }

    public double MaxGustKmH
    {
        get => edit.Weather.MaxGustKmH;
        set { edit.Weather.MaxGustKmH = value; OnPropertyChanged(); }
    }

    public int WeatherStaleAfterMin
    {
        get => edit.Weather.StaleAfterMin;
        set { edit.Weather.StaleAfterMin = value; OnPropertyChanged(); }
    }

    public int WeatherTimeoutS
    {
        get => edit.Weather.TimeoutS;
        set { edit.Weather.TimeoutS = value; OnPropertyChanged(); }
    }

    private void Save()
    {
        var errors = new List<string>(validator.Validate(edit));
        if (edit.ImageSource.Kind == ImageSourceKind.Direct && edit.ImageSource.Direct.UsePassword
            && string.IsNullOrEmpty(newPassword) && PasswordStatus.StartsWith("No password", StringComparison.Ordinal))
        {
            errors.Add("Password login is selected: enter the SFTP password.");
        }

        if (errors.Count > 0)
        {
            Message = "Not saved:\n" + string.Join("\n", errors);
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(newPassword))
            {
                credentials.SetSecret(CredentialKeys.SftpPassword, newPassword);
                newPassword = null;
                OnPropertyChanged(nameof(SftpPassword));
                OnPropertyChanged(nameof(PasswordStatus));
            }

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
        newPassword = null;
        Message = "Unsaved changes discarded.";
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Fetches one image with the settings on screen (saved or not) and reports the result.</summary>
    private async void TestImage()
    {
        if (testing) return;
        testing = true;
        ImageSourceSettings snapshot = SettingsStore.Clone(edit).ImageSource;
        Message = $"Testing the {snapshot.Kind} image source…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(snapshot.TimeoutS + 5));
            string result = await tester.TestAsync(snapshot, cts.Token);
            Message = snapshot.Direct.UsePassword && !string.IsNullOrEmpty(newPassword)
                ? result + "\n(Tested with the stored password; Save to use the new one.)"
                : result;
        }
        catch (Exception e)
        {
            Message = $"Test failed: {e.Message}";
        }
        finally
        {
            testing = false;
        }
    }

    /// <summary>Requests the current observation with the settings on screen (saved or not) and reports it.</summary>
    private async void TestWeather()
    {
        if (testing) return;
        testing = true;
        WeatherSettings snapshot = SettingsStore.Clone(edit).Weather;
        Message = $"Testing weather station {snapshot.StationId}…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(snapshot.TimeoutS + 5));
            Message = await weatherTester.TestAsync(snapshot, cts.Token);
        }
        catch (Exception e)
        {
            Message = $"Weather test failed: {e.Message}";
        }
        finally
        {
            testing = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
