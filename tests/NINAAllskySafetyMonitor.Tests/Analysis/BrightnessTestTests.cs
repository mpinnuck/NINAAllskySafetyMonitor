using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Analysis;

/// <summary>BackgroundStatistics, BrightnessTest, MoonBrightnessAdjuster and ClearSkyReferenceBuilder.</summary>
[Requirement("REQ-30", "REQ-31")]
public sealed class BrightnessTestTests
{
    private const int W = 320, H = 320;

    /// <summary>A 10 × 10 block map: level 20, non-uniformity 0.1, with the given changes.</summary>
    private static BlockStatistics Map(Func<int, int, float>? level = null, Func<int, int, float>? uniformity = null)
    {
        var med = new float[100];
        var uni = new float[100];
        for (int by = 0; by < 10; by++)
        {
            for (int bx = 0; bx < 10; bx++)
            {
                med[(by * 10) + bx] = level?.Invoke(bx, by) ?? 20f;
                uni[(by * 10) + bx] = uniformity?.Invoke(bx, by) ?? 0.1f;
            }
        }

        return new BlockStatistics(32, W, H, med, uni);
    }

    /// <summary>ROI = the top-left 3 × 3 blocks.</summary>
    private static PixelMask Roi()
    {
        var m = new PixelMask(W, H);
        for (int y = 0; y < 96; y++)
        {
            for (int x = 0; x < 96; x++) m.Add(x, y);
        }

        return m;
    }

    private static bool InRoi(int bx, int by) => bx < 3 && by < 3;

    private static BrightnessTestResult Evaluate(BlockStatistics current, ClearSkyReference? reference, double moonFactor = 1) =>
        new BrightnessTest(() => new CloudDetectionSettings()).Evaluate(current, reference, Roi(), moonFactor);

    private static ClearSkyReference Reference() => new(Map((bx, _) => 10f + bx), DateTime.UnixEpoch);

    [Fact]
    public void SkippedWithoutReference() => Assert.True(Evaluate(Map(), null).Skipped);

    [Fact]
    public void SkippedWhenTheReferenceIsForAnotherImageSize() =>
        Assert.True(Evaluate(Map(), new ClearSkyReference(new BlockStatistics(32, 640, 320, new float[200], new float[200]), DateTime.UnixEpoch)).Skipped);

    [Fact]
    public void SameSkyAtAnotherExposureIsClear()
    {
        BrightnessTestResult r = Evaluate(Map((bx, _) => (10f + bx) * 1.7f), Reference());
        Assert.False(r.Skipped);
        Assert.Equal(1.0, r.RelativeBrightness!.Value, 3);
        Assert.Equal(1.0, r.UniformityRatio!.Value, 3);
        Assert.False(r.Cloud);
    }

    [Fact]
    public void BrighterRoiIsCloud()
    {
        BrightnessTestResult r = Evaluate(Map((bx, by) => (10f + bx) * (InRoi(bx, by) ? 1.5f : 1f)), Reference());
        Assert.Equal(1.5, r.RelativeBrightness!.Value, 3);
        Assert.True(r.Cloud);
    }

    [Fact]
    public void BlotchyRoiIsCloud()
    {
        BrightnessTestResult r = Evaluate(Map((bx, _) => 10f + bx, (bx, by) => InRoi(bx, by) ? 0.2f : 0.1f), Reference());
        Assert.Equal(2.0, r.UniformityRatio!.Value, 3);
        Assert.True(r.Cloud);
    }

    [Fact]
    public void MoonRaisesTheLimits()
    {
        BlockStatistics brighter = Map((bx, by) => (10f + bx) * (InRoi(bx, by) ? 1.5f : 1f));
        Assert.False(Evaluate(brighter, Reference(), moonFactor: 1.2).Cloud);
    }

    [Theory]
    [InlineData(-10, 1.0, 1.0)]
    [InlineData(90, 0.0, 1.0)]
    [InlineData(90, 1.0, 1.5)]
    [InlineData(30, 1.0, 1.25)]
    public void MoonFactor(double alt, double illumination, double expected) =>
        Assert.Equal(expected, new MoonBrightnessAdjuster().Factor(alt, illumination), 6);

    [Fact]
    public void BackgroundIsMeasuredInsideTheAreaOnly()
    {
        var pixels = new float[64 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = (i % 64) < 32 ? 40f + (i % 5) : 99f;
        var area = new PixelMask(64, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++) area.Add(x, y);
        }

        BlockStatistics b = new BackgroundStatistics().Measure(new GrayFrame(64, 32, pixels), area);

        Assert.Equal(2, b.Columns);
        Assert.Equal(42f, b.Median[0]);
        Assert.InRange(b.Uniformity[0], 0.02f, 0.05f);
        Assert.False(b.IsValid(1));
    }

    [Fact]
    public void ReferenceNeedsAClearTunedMoonlessFrame()
    {
        var builder = new ClearSkyReferenceBuilder();
        BlockStatistics map = Map();
        var clear = new StarTestResult(200, 190, 0.95, false, false);
        Assert.NotNull(builder.TryCapture(map, clear, -10, true, DateTime.UnixEpoch));
        Assert.Null(builder.TryCapture(map, clear, -10, false, DateTime.UnixEpoch));
        Assert.Null(builder.TryCapture(map, clear, 5, true, DateTime.UnixEpoch));
        Assert.Null(builder.TryCapture(map, clear with { Matched = 170, Fraction = 0.85 }, -10, true, DateTime.UnixEpoch));
        Assert.Null(builder.TryCapture(map, clear with { Inconclusive = true }, -10, true, DateTime.UnixEpoch));
    }
}
