using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Acquisition;

[Requirement("REQ-52")]
public sealed class HostKeyVerifierTests
{
    private const string Pinned = "SHA256:xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg";

    [Theory]
    [InlineData("xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg")]
    [InlineData("xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg=")]
    [InlineData("SHA256:xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg")]
    [InlineData(" sha256:xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg ")]
    public void MatchingFingerprintIsTrusted(string received) =>
        Assert.True(new HostKeyVerifier(Pinned).Verify(received));

    [Theory]
    [InlineData("XDCRPFUWOI55OZOIKADIY7ON48U/4+PCU7EPIK0JRQG")]
    [InlineData("AAAApFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg")]
    [InlineData("")]
    [InlineData(null)]
    public void DifferentFingerprintIsRejected(string? received) =>
        Assert.False(new HostKeyVerifier(Pinned).Verify(received));

    [Fact]
    public void NoPinnedFingerprintTrustsNothing() =>
        Assert.False(new HostKeyVerifier(string.Empty).Verify("xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg"));

    [Fact]
    public void MismatchMessageNamesHostAndReceivedKey()
    {
        var e = new HostKeyMismatchException("allsky.local", "ssh-ed25519 SHA256:abc");
        Assert.Contains("allsky.local", e.Message);
        Assert.Contains("SHA256:abc", e.Message);
    }
}
