using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Analysis;

[Requirement("REQ-20", "REQ-29")]
public sealed class StarDetectorTests
{
    private static (GrayFrame Frame, List<PixelPoint> Stars, List<PixelPoint> Hot) Field()
    {
        const int size = 300;
        var rng = new Random(5);
        var pixels = new float[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = 30f + (float)((rng.NextDouble() * 3) - 1.5);
        var stars = new List<PixelPoint> { new(50.3, 60.7), new(150.5, 150.2), new(240.8, 80.4), new(100.1, 250.6) };
        double amp = 150;
        foreach (PixelPoint s in stars)
        {
            SyntheticSky.Stamp(pixels, size, s.X, s.Y, amp, 1.3);
            amp /= 2;
        }

        var hot = new List<PixelPoint> { new(20, 20), new(200, 200), new(260, 30), new(120, 40) };
        foreach (PixelPoint h in hot) pixels[((int)h.Y * size) + (int)h.X] = 255f;
        return (new GrayFrame(size, size, pixels), stars, hot);
    }

    [Fact]
    public void FindsStarsWithSubPixelCentres()
    {
        (GrayFrame frame, List<PixelPoint> stars, _) = Field();
        IReadOnlyList<DetectedStar> found = new StarDetector().Detect(frame);

        Assert.Equal(stars.Count, found.Count);
        foreach (PixelPoint s in stars)
        {
            Assert.Contains(found, d => Math.Abs(d.Position.X - s.X) < 0.4 && Math.Abs(d.Position.Y - s.Y) < 0.4);
        }
    }

    [Fact]
    public void SingleHotPixelsAreIgnored()
    {
        (GrayFrame frame, _, List<PixelPoint> hot) = Field();
        IReadOnlyList<DetectedStar> found = new StarDetector().Detect(frame);
        Assert.DoesNotContain(found, d => hot.Any(h => Math.Abs(d.Position.X - h.X) < 2 && Math.Abs(d.Position.Y - h.Y) < 2));
    }

    [Fact]
    public void BrightestPeakComesFirst()
    {
        (GrayFrame frame, List<PixelPoint> stars, _) = Field();
        IReadOnlyList<DetectedStar> found = new StarDetector().Detect(frame);
        Assert.True(Math.Abs(found[0].Position.X - stars[0].X) < 0.5);
        Assert.True(found.Zip(found.Skip(1)).All(p => p.First.Peak >= p.Second.Peak));
    }

    [Fact]
    public void MaskLimitsTheSearch()
    {
        (GrayFrame frame, List<PixelPoint> stars, _) = Field();
        var mask = new PixelMask(frame.Width, frame.Height);
        for (int y = 0; y < 150; y++)
        {
            for (int x = 0; x < 150; x++) mask.Add(x, y);
        }

        IReadOnlyList<DetectedStar> found = new StarDetector().Detect(frame, mask);
        Assert.Single(found);
        Assert.Equal(stars[0].X, found[0].Position.X, 0);
    }
}
