using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Acquisition;

[Requirement("REQ-11", "REQ-15", "REQ-16", "REQ-51", "REQ-52")]
public sealed class SftpImageSourceTests
{
    private readonly ImageSourceSettings settings = new();
    private readonly FakeRemoteFileConnector pi = new();

    private SftpImageSource Source() => new(pi, () => settings);

    [Fact]
    public async Task FirstFetchDownloadsWithModificationTimeAsCaptureTime()
    {
        FetchResult r = await Source().FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.NewImage, r.Outcome);
        Assert.Equal(pi.ModifiedUtc, r.Image!.CaptureTimeUtc);
        Assert.Equal(DateTimeKind.Utc, r.Image.CaptureTimeUtc.Kind);
        Assert.Equal(ImageSourceKind.Direct, r.Image.Source);
        Assert.Equal("/home/pi/allsky/tmp/allsky.jpg", pi.LastPath);
        Assert.Equal(1, pi.Disposed);
    }

    [Fact]
    public async Task UnchangedFileIsNotDownloadedAgain()
    {
        SftpImageSource source = Source();
        await source.FetchAsync(false, CancellationToken.None);
        FetchResult r = await source.FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.Unchanged, r.Outcome);
        Assert.Equal(1, pi.Downloads);
    }

    [Fact]
    public async Task ChangedFileIsDownloaded()
    {
        SftpImageSource source = Source();
        await source.FetchAsync(false, CancellationToken.None);
        pi.ModifiedUtc = pi.ModifiedUtc.AddMinutes(1);
        FetchResult r = await source.FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.NewImage, r.Outcome);
        Assert.Equal(2, pi.Downloads);
    }

    [Fact]
    public async Task ForceAndResetDownloadEvenWhenUnchanged()
    {
        SftpImageSource source = Source();
        await source.FetchAsync(false, CancellationToken.None);
        Assert.Equal(FetchOutcome.NewImage, (await source.FetchAsync(true, CancellationToken.None)).Outcome);

        source.Reset();
        Assert.Equal(FetchOutcome.NewImage, (await source.FetchAsync(false, CancellationToken.None)).Outcome);
        Assert.Equal(3, pi.Downloads);
    }

    [Fact]
    public async Task ConnectionErrorBecomesFailedResult()
    {
        pi.ConnectError = new IOException("Could not connect to allsky.local or 192.168.1.110: No route to host");
        FetchResult r = await Source().FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, r.Outcome);
        Assert.Contains("No route to host", r.Error);
    }

    [Fact]
    public async Task HostKeyMismatchMakesImageUnavailable()
    {
        pi.ConnectError = new HostKeyMismatchException("allsky.local", "ssh-ed25519 SHA256:other");
        FetchResult r = await Source().FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, r.Outcome);
        Assert.Contains("Host key mismatch", r.Error);
        Assert.Null(r.Image);
        Assert.True(r.Untrusted);
    }

    [Fact]
    public async Task StalledConnectionTimesOut()
    {
        settings.TimeoutS = 1;
        pi.Hang = TimeSpan.FromSeconds(3);
        FetchResult r = await Source().FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, r.Outcome);
        Assert.Contains("Timed out after 1 s", r.Error);
    }
}
