using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Plugin.Imaging;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// Calibration mode of the image viewer (UC-12, UC-13): fits the lens to the frame automatically, or from
/// two or three stars the imager names and clicks; shows the result and warnings; draws the working-area
/// outline by clicking; Save stores the lens and outline, Cancel discards them.
/// </summary>
[Requirement("REQ-19", "REQ-20", "REQ-21", "REQ-22", "REQ-47", "REQ-57")]
public sealed class CalibrationViewModel : INotifyPropertyChanged
{
    private readonly CalibrationFrame frame;
    private readonly SiteLocation site;
    private readonly LensCalibrationSession session;
    private readonly Action<LensCalibration?, IReadOnlyList<PixelPoint>> save;
    private readonly List<StarIdentification> picks = new();
    private readonly List<PixelPoint> outline;
    private LensFitResult? result;
    private string status;
    private string warnings = string.Empty;
    private bool busy;
    private bool picking;
    private bool drawing;
    private NamedStarItem? selectedStar;

    public CalibrationViewModel(
        CalibrationFrame frame,
        SiteLocation site,
        Func<LensFitSettings> fitSettings,
        LensCalibration? currentLens,
        WorkingAreaDefinition area,
        Action<LensCalibration?, IReadOnlyList<PixelPoint>> save)
    {
        this.frame = frame ?? throw new ArgumentNullException(nameof(frame));
        this.site = site ?? throw new ArgumentNullException(nameof(site));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        Area = area ?? throw new ArgumentNullException(nameof(area));
        CurrentLens = currentLens;
        session = LensCalibrationSession.Create(fitSettings);
        outline = area.Outline.ToList();
        status = string.Create(CultureInfo.CurrentCulture, $"{frame.Source}, taken {frame.CaptureUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}. Press Fit.");
        NamedStars = session.Sky(frame, site)
            .Where(s => s.Star.Name is not null && s.Position.AltDeg > 10)
            .Select(s => new NamedStarItem(s.Star, string.Create(CultureInfo.CurrentCulture, $"{s.Star.Name}  (mag {s.Star.Magnitude:0.0}, alt {s.Position.AltDeg:0}°, az {s.Position.AzDeg:0}°)")))
            .ToList();

        FitCommand = new DelegateCommand(() => _ = FitAsync(false));
        FitFromPicksCommand = new DelegateCommand(() => _ = FitAsync(true));
        ClearPicksCommand = new DelegateCommand(ClearPicks);
        ClearOutlineCommand = new DelegateCommand(ClearOutline);
        SaveCommand = new DelegateCommand(Save);
        CancelCommand = new DelegateCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the overlay should be redrawn.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised by Save and Cancel.</summary>
    public event EventHandler? CloseRequested;

    public ICommand FitCommand { get; }

    public ICommand FitFromPicksCommand { get; }

    public ICommand ClearPicksCommand { get; }

    public ICommand ClearOutlineCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    /// <summary>The working area being edited (its outline is <see cref="Outline"/>).</summary>
    public WorkingAreaDefinition Area { get; }

    /// <summary>The saved lens model, if any (used for drawing until a fit is made).</summary>
    public LensCalibration? CurrentLens { get; }

    /// <summary>The fitted lens model, or null before a successful fit.</summary>
    public LensCalibration? FittedLens => result?.Lens;

    public IReadOnlyList<PixelPoint> Outline => outline;

    public IReadOnlyList<NamedStarItem> NamedStars { get; }

    public ObservableCollection<string> PickList { get; } = new();

    public string Status
    {
        get => status;
        private set { status = value; OnPropertyChanged(); }
    }

    public string Warnings
    {
        get => warnings;
        private set { warnings = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWarnings)); }
    }

    public bool HasWarnings => warnings.Length > 0;

    public bool Busy
    {
        get => busy;
        private set { busy = value; OnPropertyChanged(); }
    }

    /// <summary>While on, a click on the image marks where <see cref="SelectedStar"/> is.</summary>
    public bool Picking
    {
        get => picking;
        set
        {
            picking = value;
            if (value) Drawing = false;
            OnPropertyChanged();
        }
    }

    /// <summary>While on, each click on the image adds a point to the outline.</summary>
    public bool Drawing
    {
        get => drawing;
        set
        {
            drawing = value;
            if (value) Picking = false;
            OnPropertyChanged();
        }
    }

    public NamedStarItem? SelectedStar
    {
        get => selectedStar;
        set { selectedStar = value; OnPropertyChanged(); }
    }

    /// <summary>A click on the image, in original image pixels.</summary>
    public void OnImageClick(PixelPoint p)
    {
        if (Picking && SelectedStar is { } item)
        {
            picks.RemoveAll(x => x.Star.Id == item.Star.Id);
            picks.Add(new StarIdentification(item.Star, p));
            RefreshPickList();
            Status = picks.Count >= 2 ? "Press Fit from identified stars, or identify another." : "Choose and click another star.";
        }
        else if (Drawing)
        {
            outline.Add(p);
            Status = $"Outline: {outline.Count} points. Click around the sky you can see; turn Draw outline off when done.";
        }
        else
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>What to draw: catalogue stars (green when matched), identified stars and the outline.</summary>
    public OverlayContent Overlay(OverlayContent baseContent)
    {
        var stars = new List<OverlayStar>();
        if (result is not null)
        {
            var projection = new LensProjection(result.Lens);
            var matched = new HashSet<int>(result.Matches.Select(m => m.Star.Star.Id));
            foreach (SkyStar s in session.Sky(frame, site).Where(s => s.Star.Magnitude <= 3.5 || matched.Contains(s.Star.Id)))
            {
                stars.Add(new OverlayStar(projection.ToPixel(s.Position), s.Star.Magnitude < 1.5 ? s.Star.Name : null, matched.Contains(s.Star.Id)));
            }
        }

        return baseContent with
        {
            WorkingArea = Area with { Outline = Drawing ? Array.Empty<PixelPoint>() : outline.ToArray() },
            DraftOutline = Drawing ? outline.ToArray() : Array.Empty<PixelPoint>(),
            Stars = stars,
            Picks = picks.Select(p => (p.Position, p.Star.Name ?? p.Star.Id.ToString(CultureInfo.InvariantCulture))).ToList(),
        };
    }

    private async Task FitAsync(bool fromPicks)
    {
        if (Busy) return;
        if (fromPicks && picks.Count < 2)
        {
            Status = "Identify at least two stars first: choose a star, turn Identify on, click it on the image.";
            return;
        }

        Busy = true;
        Status = fromPicks ? "Fitting from the identified stars…" : "Finding stars and fitting the lens…";
        try
        {
            var chosen = picks.ToList();
            CalibrationOutcome outcome = await Task.Run(() => fromPicks
                ? session.CalibrateFromIdentified(frame, site, chosen)
                : session.Calibrate(frame, site));
            result = outcome.Result;
            Status = outcome.Message;
            Warnings = string.Join(Environment.NewLine, outcome.Warnings);
            if (outcome.NeedsIdentification) Picking = true;
            OnPropertyChanged(nameof(FittedLens));
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Status = $"Calibration failed: {e.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    private void ClearPicks()
    {
        picks.Clear();
        RefreshPickList();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ClearOutline()
    {
        outline.Clear();
        Status = "Outline cleared. Turn Draw outline on and click around the sky you can see.";
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        if (outline.Count is > 0 and < 3)
        {
            Status = "An outline needs at least 3 points (or clear it).";
            return;
        }

        save(FittedLens, outline.ToArray());
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshPickList()
    {
        PickList.Clear();
        foreach (StarIdentification p in picks)
        {
            PickList.Add(string.Create(CultureInfo.CurrentCulture, $"{p.Star.Name} at ({p.Position.X:0}, {p.Position.Y:0})"));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A named star in the identify list.</summary>
public sealed record NamedStarItem(CatalogueStar Star, string Label)
{
    public override string ToString() => Label;
}
