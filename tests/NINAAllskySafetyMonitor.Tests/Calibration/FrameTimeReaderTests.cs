using NINAAllskySafetyMonitor.Core.Calibration;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Calibration;

[Requirement("REQ-20")]
public sealed class FrameTimeReaderTests
{
    private static readonly TimeZoneInfo Aedt = TimeZoneInfo.CreateCustomTimeZone("AEDT", TimeSpan.FromHours(11), "AEDT", "AEDT");
    private readonly FrameTimeReader reader = new();

    [Theory]
    [InlineData("allsky-20261020022133.jpg")]
    [InlineData(@"C:\D_Drive\AllskyFrames\20261019\allsky-20261020022133.jpg")]
    [InlineData("image-20261020022133.jpg")]
    public void FileNameTimeIsLocalTime(string name) =>
        Assert.Equal(new DateTime(2026, 10, 19, 15, 21, 33, DateTimeKind.Utc), reader.FromFileName(name, Aedt));

    [Theory]
    [InlineData("allsky.jpg")]
    [InlineData("allsky-2026102002.jpg")]
    [InlineData("allsky-20261340022133.jpg")]
    [InlineData("")]
    public void NoValidTimeGivesNull(string name) => Assert.Null(reader.FromFileName(name, Aedt));

    [Fact]
    public void ResultIsUtc() => Assert.Equal(DateTimeKind.Utc, reader.FromFileName("allsky-20261020022133.jpg", Aedt)!.Value.Kind);

    [Fact]
    public void LiveImageUsesItsCaptureTime()
    {
        var t = new DateTime(2026, 11, 2, 11, 0, 0, DateTimeKind.Utc);
        Assert.Equal(t, reader.FromSkyImage(new SkyImage(new byte[] { 1 }, t, ImageSourceKind.Direct)));
    }
}
