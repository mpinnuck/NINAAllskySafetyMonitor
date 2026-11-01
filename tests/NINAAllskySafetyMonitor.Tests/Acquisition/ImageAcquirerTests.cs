using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Acquisition;

[Requirement("REQ-14", "REQ-16", "REQ-17", "REQ-18")]
public sealed class ImageAcquirerTests
{
    private static readonly DateTime Captured = new(2026, 11, 1, 11, 59, 0, DateTimeKind.Utc);
    private readonly ImageSourceSettings settings = new();
    private readonly FakeClock clock = new(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly FakeImageSource direct = new(ImageSourceKind.Direct);
    private readonly FakeImageSource web = new(ImageSourceKind.Web);
    private readonly List<TimeSpan> delays = new();
    private readonly ListLogSink log = new();

    private ImageAcquirer Acquirer() => new(
        new ImageSourceFactory(web, direct),
        new JpegIntegrityChecker(),
        clock,
        log,
        () => settings,
        (d, _) => { delays.Add(d); return Task.CompletedTask; });

    private static FetchResult Image(byte[] bytes) => FetchResult.New(new SkyImage(bytes, Captured, ImageSourceKind.Direct));

    [Fact]
    public async Task CompleteImageIsKept()
    {
        direct.Then(Image(FakeImageSource.CompleteJpeg()));
        ImageAcquirer a = Acquirer();
        await a.AcquireAsync(CancellationToken.None);

        Assert.NotNull(a.LatestImage);
        Assert.Equal(clock.UtcNow, a.LastSuccessUtc);
        Assert.Null(a.LastError);
        Assert.Equal(1, direct.Fetches);
    }

    [Fact]
    public async Task IncompleteImageIsFetchedAgainAfterDelay()
    {
        direct.Then(Image(FakeImageSource.IncompleteJpeg())).Then(Image(FakeImageSource.CompleteJpeg()));
        ImageAcquirer a = Acquirer();
        await a.AcquireAsync(CancellationToken.None);

        Assert.Equal(2, direct.Fetches);
        Assert.True(direct.ForceFlags[1]);
        Assert.Equal(new[] { TimeSpan.FromSeconds(5) }, delays);
        Assert.NotNull(a.LatestImage);
        Assert.Null(a.LastError);
    }

    [Fact]
    public async Task ImageStillIncompleteAfterThreeAttemptsIsAnError()
    {
        direct.Then(Image(FakeImageSource.IncompleteJpeg()));
        ImageAcquirer a = Acquirer();
        await a.AcquireAsync(CancellationToken.None);

        Assert.Equal(3, direct.Fetches);
        Assert.Equal(2, delays.Count);
        Assert.Null(a.LatestImage);
        Assert.Null(a.LastSuccessUtc);
        Assert.Contains("incomplete after 3 attempts", a.LastError);
        Assert.Equal(1, direct.Resets);
    }

    [Fact]
    public async Task UnchangedImageCountsAsSuccessAndKeepsPreviousImage()
    {
        direct.Then(Image(FakeImageSource.CompleteJpeg())).Then(FetchResult.Unchanged());
        ImageAcquirer a = Acquirer();
        await a.AcquireAsync(CancellationToken.None);
        SkyImage? first = a.LatestImage;

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await a.AcquireAsync(CancellationToken.None);

        Assert.Same(first, a.LatestImage);
        Assert.Equal(clock.UtcNow, a.LastSuccessUtc);
        Assert.False(direct.ForceFlags[1]);
    }

    [Fact]
    public async Task FailureKeepsLastImageAndRecordsError()
    {
        direct.Then(Image(FakeImageSource.CompleteJpeg())).Then(FetchResult.Failed("Could not connect"));
        ImageAcquirer a = Acquirer();
        await a.AcquireAsync(CancellationToken.None);
        DateTime? success = a.LastSuccessUtc;

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await a.AcquireAsync(CancellationToken.None);

        Assert.NotNull(a.LatestImage);
        Assert.Equal(success, a.LastSuccessUtc);
        Assert.Equal("Could not connect", a.LastError);
    }

    [Fact]
    [Requirement("REQ-52")]
    public async Task HostKeyMismatchDiscardsEarlierImages()
    {
        direct.Then(Image(FakeImageSource.CompleteJpeg())).Then(FetchResult.FailedUntrusted("Host key mismatch for allsky.local"));
        ImageAcquirer a = Acquirer();
        await a.AcquireAsync(CancellationToken.None);
        Assert.NotNull(a.LatestImage);

        await a.AcquireAsync(CancellationToken.None);

        Assert.Null(a.LatestImage);
        Assert.Null(a.LastSuccessUtc);
        Assert.Contains("Host key mismatch", a.LastError);
    }

    [Fact]
    public async Task RepeatedFailureIsLoggedOnce()
    {
        direct.Then(FetchResult.Failed("Could not connect"));
        ImageAcquirer a = Acquirer();
        await a.AcquireAsync(CancellationToken.None);
        await a.AcquireAsync(CancellationToken.None);
        await a.AcquireAsync(CancellationToken.None);

        Assert.Single(log.Entries, e => e.Message.Contains("Could not connect"));
    }

    [Fact]
    public async Task SelectedSourceIsUsedAndSwitchingForcesADownload()
    {
        direct.Then(Image(FakeImageSource.CompleteJpeg()));
        web.Then(Image(FakeImageSource.CompleteJpeg()));
        ImageAcquirer a = Acquirer();

        await a.AcquireAsync(CancellationToken.None);
        settings.Kind = ImageSourceKind.Web;
        await a.AcquireAsync(CancellationToken.None);

        Assert.Equal(1, direct.Fetches);
        Assert.Equal(1, web.Fetches);
        Assert.True(web.ForceFlags[0]);
    }
}
