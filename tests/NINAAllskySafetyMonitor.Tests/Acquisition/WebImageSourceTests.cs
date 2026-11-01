using System.Net;
using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Acquisition;

[Requirement("REQ-11", "REQ-15", "REQ-51")]
public sealed class WebImageSourceTests
{
    private readonly ImageSourceSettings settings = new() { Kind = ImageSourceKind.Web };
    private readonly FakeHttpHandler server = new();

    private WebImageSource Source() => new(new HttpClient(server), () => settings);

    [Theory]
    [InlineData("https://oci.microconcepts.com.au/allsky/", "image.jpg")]
    [InlineData("https://oci.microconcepts.com.au/allsky", "image.jpg")]
    public void UrlIsBaseUrlPlusFileName(string baseUrl, string file) =>
        Assert.Equal(
            "https://oci.microconcepts.com.au/allsky/image.jpg",
            WebImageSource.ImageUrl(new WebSourceSettings { BaseUrl = baseUrl, FileName = file }).ToString());

    [Fact]
    public async Task ImageUsesServerLastModifiedAsCaptureTime()
    {
        FetchResult r = await Source().FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.NewImage, r.Outcome);
        Assert.Equal(server.LastModified!.Value.UtcDateTime, r.Image!.CaptureTimeUtc);
        Assert.Equal(ImageSourceKind.Web, r.Image.Source);
        Assert.Equal("https://oci.microconcepts.com.au/allsky/image.jpg", server.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task SecondFetchAsksIfModifiedAndAcceptsNotModified()
    {
        WebImageSource source = Source();
        await source.FetchAsync(false, CancellationToken.None);
        server.Status = HttpStatusCode.NotModified;
        FetchResult r = await source.FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.Unchanged, r.Outcome);
        Assert.Equal(server.LastModified, server.LastRequest!.Headers.IfModifiedSince);
    }

    [Fact]
    public async Task SameLastModifiedIsUnchangedEvenIfServerIgnoresTheQuestion()
    {
        WebImageSource source = Source();
        await source.FetchAsync(false, CancellationToken.None);
        Assert.Equal(FetchOutcome.Unchanged, (await source.FetchAsync(false, CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task MissingLastModifiedFailsBecauseAgeIsUnknown()
    {
        server.LastModified = null;
        FetchResult r = await Source().FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, r.Outcome);
        Assert.Contains("last-modified", r.Error);
    }

    [Fact]
    public async Task HttpErrorFails()
    {
        server.Status = HttpStatusCode.NotFound;
        FetchResult r = await Source().FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, r.Outcome);
        Assert.Contains("HTTP 404", r.Error);
    }

    [Fact]
    public async Task NetworkErrorFails()
    {
        server.Throw = true;
        FetchResult r = await Source().FetchAsync(false, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, r.Outcome);
        Assert.Contains("Name or service not known", r.Error);
    }
}
