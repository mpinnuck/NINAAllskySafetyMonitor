using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Acquisition;

[Requirement("REQ-11")]
public sealed class ImageSourceFactoryTests
{
    [Fact]
    public void ReturnsTheSelectedSource()
    {
        var web = new FakeImageSource(ImageSourceKind.Web);
        var direct = new FakeImageSource(ImageSourceKind.Direct);
        var factory = new ImageSourceFactory(web, direct);

        Assert.Same(web, factory.For(ImageSourceKind.Web));
        Assert.Same(direct, factory.For(ImageSourceKind.Direct));
        Assert.Throws<ArgumentOutOfRangeException>(() => factory.For(ImageSourceKind.Replay));
    }
}
