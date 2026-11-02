using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Core.Tuning;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>
/// Tonight's history for the status panel (OxyPlot): star fraction and relative brightness against local
/// time, with the safe / unsafe state as a step line. Starts again each night; kept for this NINA session.
/// </summary>
[Requirement("REQ-45")]
public sealed class HistoryChartViewModel
{
    private static readonly OxyColor Text = OxyColor.FromRgb(0xC8, 0xCC, 0xD4);
    private static readonly OxyColor GridLine = OxyColor.FromArgb(0x40, 0xC8, 0xCC, 0xD4);
    private readonly LineSeries stars;
    private readonly LineSeries brightness;
    private readonly StairStepSeries safe;
    private MonitorSnapshot? last;
    private DateTime? night;

    public HistoryChartViewModel()
    {
        stars = new LineSeries { Title = "Star fraction", Color = OxyColor.FromRgb(0x4F, 0xC3, 0xF7), StrokeThickness = 1.5 };
        brightness = new LineSeries { Title = "Relative brightness", Color = OxyColor.FromRgb(0xFF, 0xD5, 0x4F), StrokeThickness = 1.5 };
        safe = new StairStepSeries { Title = "Safe (high) / unsafe (low)", Color = OxyColor.FromRgb(0x4C, 0xE0, 0x6A), StrokeThickness = 1, VerticalStrokeThickness = 0.5 };

        Model = new PlotModel { TextColor = Text, PlotAreaBorderColor = GridLine, PlotMargins = new OxyThickness(32, 4, 8, 20) };
        Model.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm",
            TextColor = Text,
            TicklineColor = GridLine,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = GridLine,
        });
        Model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,
            Maximum = 2,
            MajorStep = 0.5,
            TextColor = Text,
            TicklineColor = GridLine,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = GridLine,
        });
        Model.Legends.Add(new Legend { LegendPosition = LegendPosition.TopLeft, LegendTextColor = Text, LegendFontSize = 10, LegendBackground = OxyColor.FromArgb(0x80, 0x20, 0x24, 0x2C) });
        Model.Series.Add(safe);
        Model.Series.Add(stars);
        Model.Series.Add(brightness);
    }

    public PlotModel Model { get; }

    /// <summary>True once tonight has at least one point.</summary>
    public bool HasData => safe.Points.Count > 0;

    /// <summary>Adds the snapshot (once) and redraws. Idle snapshots are not plotted.</summary>
    public void Add(MonitorSnapshot? snapshot)
    {
        if (snapshot is null || ReferenceEquals(snapshot, last) || snapshot.Status.State == SafetyState.Idle) return;
        last = snapshot;

        DateTime local = snapshot.Status.TimestampUtc.ToLocalTime();
        DateTime thisNight = TuningRecorder.NightOf(local);
        if (night != thisNight)
        {
            night = thisNight;
            stars.Points.Clear();
            brightness.Points.Clear();
            safe.Points.Clear();
        }

        double x = DateTimeAxis.ToDouble(local);
        if (snapshot.Cloud?.StarFraction is double f && !snapshot.Cloud.StarTestInconclusive) stars.Points.Add(new DataPoint(x, f));
        if (snapshot.Cloud?.RelativeBrightness is double b) brightness.Points.Add(new DataPoint(x, Math.Min(b, 2)));
        safe.Points.Add(new DataPoint(x, snapshot.Status.IsSafe ? 1.9 : 0.05));
        Model.InvalidatePlot(true);
    }
}
