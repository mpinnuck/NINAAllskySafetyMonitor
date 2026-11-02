using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Measures the sky background in 32 px blocks over the working area. For each block with at least half
/// its pixels in the working area: the median, and the non-uniformity = (84th − 16th percentile) ÷ 2 ÷
/// median. Percentiles ignore the few pixels of stars and hot pixels, so stars do not need removing first.
/// </summary>
[Requirement("REQ-30")]
public sealed class BackgroundStatistics
{
    /// <summary>Block size in pixels.</summary>
    public const int BlockSize = 32;

    public BlockStatistics Measure(GrayFrame frame, PixelMask areaMask)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(areaMask);

        int cols = (frame.Width + BlockSize - 1) / BlockSize, rows = (frame.Height + BlockSize - 1) / BlockSize;
        var median = new float[cols * rows];
        var uniformity = new float[cols * rows];
        var buf = new float[BlockSize * BlockSize];
        for (int by = 0; by < rows; by++)
        {
            for (int bx = 0; bx < cols; bx++)
            {
                int n = 0, total = 0;
                for (int y = by * BlockSize; y < Math.Min(frame.Height, (by + 1) * BlockSize); y++)
                {
                    for (int x = bx * BlockSize; x < Math.Min(frame.Width, (bx + 1) * BlockSize); x++)
                    {
                        total++;
                        if (areaMask.Contains(x, y)) buf[n++] = frame.At(x, y);
                    }
                }

                int i = (by * cols) + bx;
                if (n < Math.Max(16, total / 2))
                {
                    median[i] = float.NaN;
                    uniformity[i] = float.NaN;
                    continue;
                }

                Array.Sort(buf, 0, n);
                float med = buf[n / 2];
                float spread = (buf[(int)(n * 0.84)] - buf[(int)(n * 0.16)]) / 2f;
                median[i] = med;
                uniformity[i] = spread / Math.Max(med, 1f);
            }
        }

        return new BlockStatistics(BlockSize, frame.Width, frame.Height, median, uniformity);
    }
}
