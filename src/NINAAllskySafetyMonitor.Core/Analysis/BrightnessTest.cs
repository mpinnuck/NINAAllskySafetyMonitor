using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Secondary cloud test against the clear-sky reference, independent of exposure (Allsky adjusts its
/// exposure to keep the overall brightness steady). For each block k = current median ÷ reference median.
/// Relative brightness = median k in the ROI ÷ median k over the whole working area; non-uniformity ratio
/// = median over the ROI of current ÷ reference non-uniformity (already scale-free). Either above its
/// limit, raised by the Moon factor, means cloud. Skipped when there is no reference, it was taken at a
/// different image size, or the ROI has fewer than <see cref="MinRoiBlocks"/> measured blocks. When the
/// whole working area is judged, the relative brightness is 1 by definition and only the non-uniformity
/// ratio can show cloud.
/// </summary>
[Requirement("REQ-30", "REQ-31")]
public sealed class BrightnessTest
{
    /// <summary>Minimum measured blocks in the ROI.</summary>
    public const int MinRoiBlocks = 3;

    private readonly Func<CloudDetectionSettings> settings;

    public BrightnessTest(Func<CloudDetectionSettings> settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public BrightnessTestResult Evaluate(BlockStatistics current, ClearSkyReference? reference, PixelMask roiMask, double moonFactor)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(roiMask);
        var skipped = new BrightnessTestResult(null, null, true, false);
        if (reference is null || !current.SameGridAs(reference.Blocks)) return skipped;

        BlockStatistics refBlocks = reference.Blocks;
        var all = new List<double>();
        var roi = new List<double>();
        var uniformity = new List<double>();
        int half = current.BlockSize / 2;
        for (int by = 0; by < current.Rows; by++)
        {
            for (int bx = 0; bx < current.Columns; bx++)
            {
                int i = (by * current.Columns) + bx;
                if (!current.IsValid(i) || !refBlocks.IsValid(i) || refBlocks.Median[i] < 1f) continue;
                double k = current.Median[i] / refBlocks.Median[i];
                all.Add(k);
                int cx = Math.Min((bx * current.BlockSize) + half, current.FrameWidth - 1);
                int cy = Math.Min((by * current.BlockSize) + half, current.FrameHeight - 1);
                if (!roiMask.Contains(cx, cy)) continue;
                roi.Add(k);
                if (refBlocks.Uniformity[i] > 0.001f) uniformity.Add(current.Uniformity[i] / refBlocks.Uniformity[i]);
            }
        }

        if (roi.Count < MinRoiBlocks || all.Count == 0) return skipped;

        double relative = Median(roi) / Math.Max(Median(all), 1e-6);
        double? uniformityRatio = uniformity.Count >= MinRoiBlocks ? Median(uniformity) : null;
        CloudDetectionSettings s = settings();
        double f = Math.Max(1.0, moonFactor);
        bool cloud = relative > s.MaxBrightnessRatio * f || uniformityRatio > s.MaxUniformityRatio * f;
        return new BrightnessTestResult(relative, uniformityRatio, false, cloud);
    }

    internal static double Median(List<double> values)
    {
        values.Sort();
        int n = values.Count;
        return n % 2 == 1 ? values[n / 2] : (values[(n / 2) - 1] + values[n / 2]) / 2;
    }
}
