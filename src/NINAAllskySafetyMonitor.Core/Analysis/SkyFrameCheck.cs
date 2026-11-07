using System.Globalization;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Recognises an image that is not a sky frame, such as the notice image Allsky shows when its software
/// is not running. Such an image is drawn, so inside the working area it is flat: the pixel-to-pixel noise
/// is below <see cref="MaxFlatNoise"/> grey levels and more than <see cref="MinFlatFraction"/> of the pixels
/// are within ±1 of one value. A real frame always has sensor noise (about 1.1 grey levels on the test
/// frame). An image whose size differs from the frame the lens was calibrated on is not a sky frame
/// either. The result is a NoCloud check, so a notice image is reported unsafe with that reason.
/// </summary>
[Requirement("REQ-50", "REQ-60")]
public sealed class SkyFrameCheck
{
    /// <summary>Below this pixel-to-pixel noise (grey levels) an image counts as flat.</summary>
    public const double MaxFlatNoise = 0.5;

    /// <summary>Above this fraction of pixels within ±1 of one value an image counts as flat.</summary>
    public const double MinFlatFraction = 0.5;

    /// <summary>Spacing of the sample grid in pixels.</summary>
    internal const int Step = 8;

    private const string NotSky = "Not a sky frame (Allsky notice image?)";

    /// <summary>Checks the frame; passes when it looks like a sky frame.</summary>
    public ConditionCheck Evaluate(GrayFrame frame, WorkingArea area, LensCalibration lens)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(lens);

        if (lens.ImageWidth > 0 && lens.ImageHeight > 0 && (lens.ImageWidth != frame.Width || lens.ImageHeight != frame.Height))
        {
            return Fail(string.Create(CultureInfo.InvariantCulture,
                $"Not a sky frame: image is {frame.Width} × {frame.Height}, the lens was calibrated on {lens.ImageWidth} × {lens.ImageHeight}"));
        }

        var projection = new LensProjection(lens);
        var differences = new List<float>();
        var levels = new int[257];
        int samples = 0;
        for (int y = Step / 2; y < frame.Height; y += Step)
        {
            for (int x = Step / 2; x < frame.Width - 1; x += Step)
            {
                if (!area.Contains(projection.ToHorizontal(new PixelPoint(x, y)))) continue;
                float v = frame.At(x, y);
                differences.Add(frame.At(x + 1, y) - v);
                levels[Math.Clamp((int)Math.Round(v), 0, 256)]++;
                samples++;
            }
        }

        if (samples < 50) return Fail("Not a sky frame: the working area is outside the image");

        double noise = RobustSigma(differences) / Math.Sqrt(2);
        int mostCommon = 0;
        for (int v = 1; v < 256; v++) mostCommon = Math.Max(mostCommon, levels[v - 1] + levels[v] + levels[v + 1]);
        double flatFraction = (double)mostCommon / samples;

        return noise < MaxFlatNoise && flatFraction > MinFlatFraction
            ? Fail(NotSky)
            : new ConditionCheck(SafetyCondition.NoCloud, true, "Sky frame");
    }

    private static ConditionCheck Fail(string reason) => new(SafetyCondition.NoCloud, false, reason);

    /// <summary>1.4826 × the median absolute deviation: the standard deviation, unaffected by stars.</summary>
    internal static double RobustSigma(List<float> values)
    {
        values.Sort();
        float median = values[values.Count / 2];
        var deviations = values.Select(v => Math.Abs(v - median)).ToList();
        deviations.Sort();
        return 1.4826 * deviations[deviations.Count / 2];
    }
}
