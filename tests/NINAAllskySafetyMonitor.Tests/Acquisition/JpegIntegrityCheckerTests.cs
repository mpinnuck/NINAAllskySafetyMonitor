using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Acquisition;

[Requirement("REQ-17")]
public sealed class JpegIntegrityCheckerTests
{
    private readonly JpegIntegrityChecker checker = new();

    [Fact]
    public void WholeJpegIsComplete() =>
        Assert.True(checker.IsComplete(new byte[] { 0xFF, 0xD8, 0x10, 0x20, 0xFF, 0xD9 }));

    [Fact]
    public void TrailingZeroPaddingIsAllowed() =>
        Assert.True(checker.IsComplete(new byte[] { 0xFF, 0xD8, 0x10, 0xFF, 0xD9, 0x00, 0x00 }));

    [Fact]
    public void MissingEndMarkerIsIncomplete() =>
        Assert.False(checker.IsComplete(new byte[] { 0xFF, 0xD8, 0x10, 0x20, 0x30, 0x40 }));

    [Fact]
    public void NotAJpegIsIncomplete() =>
        Assert.False(checker.IsComplete(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0xFF, 0xD9 }));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void TooShortOrEmptyIsIncomplete(int length) =>
        Assert.False(checker.IsComplete(new byte[length]));

    [Fact]
    public void NullIsIncomplete() => Assert.False(checker.IsComplete(null));
}
