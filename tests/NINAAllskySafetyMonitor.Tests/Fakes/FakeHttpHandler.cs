using System.Net;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>HTTP handler returning a prepared response and recording the request.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public byte[] Body { get; set; } = FakeImageSource.CompleteJpeg();

    public DateTimeOffset? LastModified { get; set; } = new DateTimeOffset(2026, 11, 1, 11, 0, 0, TimeSpan.Zero);

    public bool Throw { get; set; }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (Throw) throw new HttpRequestException("Name or service not known");

        var response = new HttpResponseMessage(Status) { Content = new ByteArrayContent(Body) };
        response.Content.Headers.LastModified = LastModified;
        return Task.FromResult(response);
    }
}
