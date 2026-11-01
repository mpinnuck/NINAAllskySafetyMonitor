using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Domain;

[Requirement("REQ-23", "REQ-25")]
public sealed class PixelMaskTests
{
    [Fact]
    public void AddedPixelsAreContainedAndCounted()
    {
        var mask = new PixelMask(10, 5);
        mask.Add(3, 2);
        mask.Add(3, 2);
        mask.Add(9, 4);

        Assert.True(mask.Contains(3, 2));
        Assert.True(mask.Contains(9, 4));
        Assert.False(mask.Contains(0, 0));
        Assert.Equal(2, mask.Count);
    }

    [Fact]
    public void PixelsOutsideTheImageAreIgnored()
    {
        var mask = new PixelMask(10, 5);
        mask.Add(-1, 0);
        mask.Add(10, 0);
        mask.Add(0, 5);

        Assert.Equal(0, mask.Count);
        Assert.False(mask.Contains(-1, 0));
    }

    [Fact]
    public void ZeroSizeIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelMask(0, 5));
}
