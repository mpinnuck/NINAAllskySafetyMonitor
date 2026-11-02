using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Captures the clear-sky reference when the thresholds are marked tuned, the Moon is down, and the star
/// test over the whole working area finds at least <see cref="MinClearFraction"/> of the expected stars.
/// </summary>
[Requirement("REQ-30")]
public sealed class ClearSkyReferenceBuilder
{
    /// <summary>Star fraction over the working area needed to call the sky clear.</summary>
    public const double MinClearFraction = 0.9;

    /// <summary>Minimum measured blocks for a usable map.</summary>
    public const int MinBlocks = 20;

    /// <summary>True when the conditions for a capture could be met (worth running the whole-area star test).</summary>
    public bool CanCapture(bool tuned, double moonAltDeg) => tuned && moonAltDeg < 0;

    public ClearSkyReference? TryCapture(BlockStatistics blocks, StarTestResult wholeArea, double moonAltDeg, bool tuned, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(wholeArea);
        if (!CanCapture(tuned, moonAltDeg) || wholeArea.Inconclusive || !(wholeArea.Fraction >= MinClearFraction)) return null;
        int valid = Enumerable.Range(0, blocks.Median.Length).Count(blocks.IsValid);
        return valid < MinBlocks ? null : new ClearSkyReference(blocks, utc);
    }
}
