using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Weather;
using NINAAllskySafetyMonitor.Plugin.Imaging;
using NINAAllskySafetyMonitor.Plugin.Nina;
using NINAAllskySafetyMonitor.Plugin.Views;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// Settings editor for the plugin options page. Edits a copy of the saved settings; Save validates
/// and persists them, Revert discards unsaved edits. The SFTP password goes to the credential store,
/// never into the settings. The clear-sky reference is captured by the monitor and kept on Save; it is
/// cleared only with its own button. Each slice adds the settings it uses.
/// </summary>
[Requirement("REQ-19", "REQ-20", "REQ-22", "REQ-23", "REQ-27", "REQ-29", "REQ-30", "REQ-31", "REQ-41", "REQ-43", "REQ-46", "REQ-47", "REQ-50", "REQ-53", "REQ-55", "REQ-58")]
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly PluginSettingsStore store;
    private readonly ICredentialStore credentials;
    private readonly ImageFetchTester tester;
    private readonly WeatherFetchTester weatherTester = new(new SystemClock());
    private readonly SettingsValidator validator = new();
    private readonly FrameTimeReader timeReader = new();
    private readonly Func<SiteLocation> site;
    private readonly IFrameDecoder decoder;
    private MonitorSettings edit;
    private string? newPassword;
    private string? newWeatherApiKey;
    private string? credentialWarning;
    private string message = string.Empty;
    private bool testing;

    public SettingsViewModel(PluginSettingsStore store, ICredentialStore credentials, Func<SiteLocation> site, IFrameDecoder decoder)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.site = site ?? throw new ArgumentNullException(nameof(site));
        this.decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        tester = new ImageFetchTester(credentials, new NinaLogSink(), new SystemClock());
        edit = SettingsStore.Clone(store.Current);
        if (!string.IsNullOrEmpty(edit.Weather.ApiKey))
        {
            try
            {
                credentials.SetSecret(CredentialKeys.WeatherApiKey, edit.Weather.ApiKey);
                NinaLogSink.RefreshSecrets();
                edit.Weather.ApiKey = string.Empty;
                store.Save(edit);
            }
            catch (Exception e)
            {
                credentialWarning = $"Could not move the saved Weather.com API key into Credential Manager: {e.Message}";
            }
        }
        SaveCommand = new DelegateCommand(Save);
        RevertCommand = new DelegateCommand(Revert);
        TestImageCommand = new DelegateCommand(TestImage);
        TestWeatherCommand = new DelegateCommand(TestWeather);
        CalibrateFromFrameCommand = new DelegateCommand(CalibrateFromFrame);
        CalibrateFromCurrentImageCommand = new DelegateCommand(CalibrateFromCurrentImage);
        ClearReferenceCommand = new DelegateCommand(ClearReference);
        store.Saved += (_, _) =>
        {
            OnPropertyChanged(nameof(ReferenceStatusText));
            OnPropertyChanged(nameof(SettingsLoadWarning));
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SaveCommand { get; }

    /// <summary>Deletes the clear-sky reference, so a new one is captured on the next tuned clear night.</summary>
    public ICommand ClearReferenceCommand { get; }

    public ICommand RevertCommand { get; }

    public ICommand TestImageCommand { get; }

    public ICommand TestWeatherCommand { get; }

    /// <summary>Opens a saved clear frame (time from its file name) in the calibration viewer.</summary>
    public ICommand CalibrateFromFrameCommand { get; }

    /// <summary>Opens the monitor's latest image in the calibration viewer.</summary>
    public ICommand CalibrateFromCurrentImageCommand { get; }

    /// <summary>Where the settings are stored.</summary>
    public string SettingsFilePath => store.FilePath;

    public string? SettingsLoadWarning => store.LoadError;

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

    /// <summary>Report safe when a condition can't be determined (REQ-50).</summary>
    public bool UndeterminedIsSafe
    {
        get => edit.Decision.UndeterminedIsSafe;
        set { edit.Decision.UndeterminedIsSafe = value; OnPropertyChanged(); }
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

    // ---- Lens calibration (REQ-19, REQ-20) ----

    private string calibrationMessage = string.Empty;

    /// <summary>Result of the last calibration action, shown under the calibration buttons.</summary>
    public string CalibrationMessage
    {
        get => calibrationMessage;
        private set { calibrationMessage = value; OnPropertyChanged(); }
    }

    /// <summary>What lens model is in use.</summary>
    public string CalibrationStatusText
    {
        get
        {
            LensCalibration? lens = edit.Calibration.Lens;
            int outline = edit.Calibration.WorkingArea.Outline.Count;
            string area = outline >= 3 ? $" Working-area outline: {outline} points." : " No working-area outline drawn.";
            if (lens is null) return "Not calibrated: cloud assessment is unavailable until a lens is calibrated." + area;
            if (lens.Manual) return "Lens model entered by hand." + area;
            return string.Create(CultureInfo.CurrentCulture,
                $"Calibrated from {lens.StarsMatched} stars in a frame taken {lens.CalibratedUtc.ToLocalTime():yyyy-MM-dd HH:mm}, error {lens.RmsErrorPx:0.0} px ({lens.RmsErrorDeg:0.00}°).") + area;
        }
    }

    public int MinMatchedStars
    {
        get => edit.LensFit.MinMatchedStars;
        set { edit.LensFit.MinMatchedStars = value; OnPropertyChanged(); }
    }

    public double MaxFitErrorDeg
    {
        get => edit.LensFit.MaxFitErrorDeg;
        set { edit.LensFit.MaxFitErrorDeg = value; OnPropertyChanged(); }
    }

    public double CatalogueLimitMagnitude
    {
        get => edit.LensFit.CatalogueLimitMagnitude;
        set { edit.LensFit.CatalogueLimitMagnitude = value; OnPropertyChanged(); }
    }

    /// <summary>On: use the values below (replaces a calibrated model). Off: restore the previously calibrated model.</summary>
    public bool ManualLensEnabled
    {
        get => edit.Calibration.Lens is { Manual: true };
        set
        {
            if (value == ManualLensEnabled) return;
            if (value)
            {
                fittedLens = edit.Calibration.Lens;
                edit.Calibration.Lens = StartingLens();
            }
            else
            {
                edit.Calibration.Lens = fittedLens;
                fittedLens = null;
            }

            OnPropertyChanged(string.Empty);
        }
    }

    private LensCalibration? fittedLens;

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
        if (edit.Calibration.Lens is not { Manual: true }) return;
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

    // ---- Cloud detection (REQ-28, REQ-29 to REQ-31, REQ-43) ----

    /// <summary>When true, detected cloud in the ROI makes the status unsafe (REQ-28).</summary>
    public bool CloudDecidesSafety
    {
        get => edit.Cloud.DecidesSafety;
        set { edit.Cloud.DecidesSafety = value; OnPropertyChanged(); }
    }

    public double LimitingMagnitude
    {
        get => edit.Cloud.LimitingMagnitude;
        set { edit.Cloud.LimitingMagnitude = value; OnPropertyChanged(); }
    }

    /// <summary>Minimum star fraction as a percentage.</summary>
    public double MinStarPercent
    {
        get => Math.Round(edit.Cloud.MinStarFraction * 100, 1);
        set { edit.Cloud.MinStarFraction = value / 100.0; OnPropertyChanged(); }
    }

    public int MinExpectedStars
    {
        get => edit.Cloud.MinExpectedStars;
        set { edit.Cloud.MinExpectedStars = value; OnPropertyChanged(); }
    }

    public double MaxBrightnessRatio
    {
        get => edit.Cloud.MaxBrightnessRatio;
        set { edit.Cloud.MaxBrightnessRatio = value; OnPropertyChanged(); }
    }

    public double MaxUniformityRatio
    {
        get => edit.Cloud.MaxUniformityRatio;
        set { edit.Cloud.MaxUniformityRatio = value; OnPropertyChanged(); }
    }

    public double MoonExclusionDeg
    {
        get => edit.Cloud.MoonExclusionDeg;
        set { edit.Cloud.MoonExclusionDeg = value; OnPropertyChanged(); }
    }

    /// <summary>Thresholds marked as tuned: clears the UNTUNED banner and allows the reference capture.</summary>
    public bool CloudTuned
    {
        get => edit.Cloud.Tuned;
        set { edit.Cloud.Tuned = value; OnPropertyChanged(); }
    }

    /// <summary>When the clear-sky reference was captured, or how it will be.</summary>
    public string ReferenceStatusText => MetricsViewModel.Reference(store.Current.Calibration.ClearSkyReference, store.Current.Cloud.Tuned);

    // ---- Tuning mode (REQ-41) ----

    public bool TuningEnabled
    {
        get => edit.Tuning.Enabled;
        set { edit.Tuning.Enabled = value; OnPropertyChanged(); }
    }

    public string TuningFolder
    {
        get => edit.Tuning.OutputFolder;
        set { edit.Tuning.OutputFolder = value; OnPropertyChanged(); }
    }

    public int TuningRetentionDays
    {
        get => edit.Tuning.RetentionDays;
        set { edit.Tuning.RetentionDays = value; OnPropertyChanged(); }
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
        get
        {
            if (newWeatherApiKey is not null) return newWeatherApiKey;
            try
            {
                return credentials.GetSecret(CredentialKeys.WeatherApiKey) ?? edit.Weather.ApiKey;
            }
            catch
            {
                return edit.Weather.ApiKey;
            }
        }
        set
        {
            newWeatherApiKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WeatherApiKeyStatus));
        }
    }

    public string WeatherApiKeyStatus
    {
        get
        {
            try
            {
                bool stored = !string.IsNullOrEmpty(credentials.GetSecret(CredentialKeys.WeatherApiKey));
                return credentialWarning ?? (stored ? "Stored in Windows Credential Manager." : "No API key stored.");
            }
            catch (Exception e)
            {
                return $"Credential Manager unavailable: {e.Message}";
            }
        }
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

    private async void CalibrateFromFrame()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a clear, moonless Allsky frame",
            Filter = "JPEG images (*.jpg;*.jpeg)|*.jpg;*.jpeg|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true) return;

        DateTime? utc = timeReader.FromFileName(dialog.FileName);
        if (utc is null)
        {
            CalibrationMessage = "The file name has no time in it (expected …-yyyyMMddHHmmss.jpg, as Allsky saves them). Rename it with its local time, or use Calibrate from current image.";
            return;
        }

        try
        {
            byte[] jpeg = await File.ReadAllBytesAsync(dialog.FileName);
            await OpenCalibrationAsync(jpeg, utc.Value, Path.GetFileName(dialog.FileName));
        }
        catch (Exception e)
        {
            CalibrationMessage = $"Could not open {Path.GetFileName(dialog.FileName)}: {e.Message}";
        }
    }

    private async void CalibrateFromCurrentImage()
    {
        SkyImage? image = MonitorHost.Instance.LatestSnapshot?.Image;
        if (image is null)
        {
            CalibrationMessage = "No current image: connect the monitor (in daylight set the Sun limit to +90), or use Calibrate from a frame.";
            return;
        }

        try
        {
            await OpenCalibrationAsync(image.JpegBytes, timeReader.FromSkyImage(image), "Current image");
        }
        catch (Exception e)
        {
            CalibrationMessage = $"Could not open the current image: {e.Message}";
        }
    }

    private async Task OpenCalibrationAsync(byte[] jpeg, DateTime captureUtc, string source)
    {
        CalibrationMessage = $"Loading {source}…";
        (GrayFrame gray, BitmapSource display) = await Task.Run(() => (decoder.Decode(jpeg), JpegBitmap.Load(jpeg)));
        var frame = new CalibrationFrame(gray, captureUtc, source);
        MonitorSettings saved = store.Current;
        var calibration = new CalibrationViewModel(frame, site(), () => edit.LensFit, saved.Calibration.Lens, saved.Calibration.WorkingArea, SaveCalibration);
        ImageViewerWindow.Open(ImageViewerViewModel.ForCalibration(display, calibration));
        CalibrationMessage = string.Create(CultureInfo.CurrentCulture, $"{source}: taken {captureUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}. Calibrate in the viewer window.");
    }

    /// <summary>Saves a fitted lens (if any) and the outline straight to the settings file.</summary>
    private void SaveCalibration(LensCalibration? lens, IReadOnlyList<PixelPoint> outline)
    {
        try
        {
            MonitorSettings s = SettingsStore.Clone(store.Current);
            if (lens is not null) s.Calibration.Lens = lens;
            s.Calibration.WorkingArea = s.Calibration.WorkingArea with { Outline = outline.ToArray() };
            store.Save(s);

            if (lens is not null) edit.Calibration.Lens = lens;
            edit.Calibration.WorkingArea = edit.Calibration.WorkingArea with { Outline = outline.ToArray() };
            OnPropertyChanged(string.Empty);
            CalibrationMessage = $"Calibration saved {DateTime.Now:HH:mm:ss}. The monitor uses it from its next cycle.";
        }
        catch (Exception e)
        {
            CalibrationMessage = $"Could not save the calibration: {e.Message}";
        }
    }

    private void Save()
    {
        edit.Weather.ApiKey = WeatherApiKey;
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
                NinaLogSink.RefreshSecrets();
                newPassword = null;
                OnPropertyChanged(nameof(SftpPassword));
                OnPropertyChanged(nameof(PasswordStatus));
            }

            credentials.SetSecret(CredentialKeys.WeatherApiKey, edit.Weather.ApiKey);
            NinaLogSink.RefreshSecrets();
            edit.Weather.ApiKey = string.Empty;
            newWeatherApiKey = null;
            OnPropertyChanged(nameof(WeatherApiKey));
            OnPropertyChanged(nameof(WeatherApiKeyStatus));

            // The reference is captured by the monitor while this page may be open: keep the saved one.
            edit.Calibration.ClearSkyReference = store.Current.Calibration.ClearSkyReference;
            store.Save(edit);
            string recovery = store.CorruptSettingsBackupPath is { } path
                ? $" The unreadable previous file was preserved at {path}."
                : string.Empty;
            Message = $"Saved {DateTime.Now:HH:mm:ss}. The monitor uses the new settings from its next cycle.{recovery}";
        }
        catch (Exception e)
        {
            Message = $"Could not save settings: {e.Message}";
        }
    }

    private void ClearReference()
    {
        try
        {
            store.SaveReference(null);
            edit.Calibration.ClearSkyReference = null;
            Message = $"Clear-sky reference cleared {DateTime.Now:HH:mm:ss}. Until a new one is captured the star test decides alone.";
        }
        catch (Exception e)
        {
            Message = $"Could not clear the clear-sky reference: {e.Message}";
        }

        OnPropertyChanged(nameof(ReferenceStatusText));
    }

    private void Revert()
    {
        edit = SettingsStore.Clone(store.Current);
        newPassword = null;
        newWeatherApiKey = null;
        fittedLens = null;
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
        snapshot.ApiKey = WeatherApiKey;
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
