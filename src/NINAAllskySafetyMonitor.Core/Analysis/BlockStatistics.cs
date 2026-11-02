using System.Text.Json.Serialization;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Sky background over the working area, block by block: the median level and the non-uniformity
/// (spread ÷ median) of each <see cref="BlockSize"/> px block. Blocks mostly outside the working area
/// hold NaN. Row-major, <see cref="Columns"/> × <see cref="Rows"/>.
/// </summary>
/// <param name="BlockSize">Block size in pixels.</param>
/// <param name="FrameWidth">Width of the measured frame.</param>
/// <param name="FrameHeight">Height of the measured frame.</param>
/// <param name="Median">Median level per block.</param>
/// <param name="Uniformity">Non-uniformity per block.</param>
[Requirement("REQ-30")]
public sealed record BlockStatistics(
    int BlockSize,
    int FrameWidth,
    int FrameHeight,
    [property: JsonConverter(typeof(CompactFloatArrayConverter))] float[] Median,
    [property: JsonConverter(typeof(CompactFloatArrayConverter))] float[] Uniformity)
{
    [JsonIgnore]
    public int Columns => (FrameWidth + BlockSize - 1) / BlockSize;

    [JsonIgnore]
    public int Rows => (FrameHeight + BlockSize - 1) / BlockSize;

    /// <summary>True when the block was measured.</summary>
    public bool IsValid(int index) => float.IsFinite(Median[index]) && float.IsFinite(Uniformity[index]);

    /// <summary>True when both maps cover the same frame size with the same blocks.</summary>
    public bool SameGridAs(BlockStatistics other) =>
        other is not null && other.BlockSize == BlockSize && other.FrameWidth == FrameWidth && other.FrameHeight == FrameHeight
        && other.Median.Length == Median.Length && other.Uniformity.Length == Uniformity.Length;
}
