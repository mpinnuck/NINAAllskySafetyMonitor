using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>Background statistics of a clear sky, captured automatically on the first tuned clear night.</summary>
[Requirement("REQ-30", "REQ-57")]
public sealed record ClearSkyReference
{
    /// <summary>Median background level of the reference frame.</summary>
    public double Median { get; init; }

    /// <summary>Non-uniformity (standard deviation ÷ median, stars removed) of the reference frame.</summary>
    public double Uniformity { get; init; }

    /// <summary>When the reference was captured.</summary>
    public DateTime CapturedUtc { get; init; }
}
