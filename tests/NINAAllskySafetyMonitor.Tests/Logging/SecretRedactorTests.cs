using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Logging;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Logging;

[Requirement("REQ-54")]
public sealed class SecretRedactorTests
{
    [Fact]
    public void SecretsAreReplaced()
    {
        var r = new SecretRedactor(() => new[] { "abc123secret", "pw-xyz" });
        Assert.Equal("key=*** pass=***", r.Redact("key=abc123secret pass=pw-xyz"));
    }

    [Fact]
    public void EmptyOrVeryShortSecretsAreIgnored()
    {
        var r = new SecretRedactor(() => new[] { null, "", "ab" });
        Assert.Equal("about abc", r.Redact("about abc"));
    }

    [Fact]
    public void ChangedSecretsArePickedUp()
    {
        string key = "first-key";
        var r = new SecretRedactor(() => new[] { key });
        key = "second-key";
        Assert.Equal("***", r.Redact("second-key"));
    }

    [Fact]
    public void UnreadableSecretsNeverBreakLogging()
    {
        var r = new SecretRedactor(() => throw new InvalidOperationException("Credential Manager unavailable"));
        Assert.Equal("[redaction unavailable; message suppressed]", r.Redact("message"));
    }

    [Fact]
    public void LogSinkRedactsBeforeWriting()
    {
        var inner = new ListLogSink();
        new RedactingLogSink(inner, new SecretRedactor(() => new[] { "abc123secret" })).Write(LogLevel.Warning, "failed for apiKey=abc123secret");
        Assert.Equal("failed for apiKey=***", inner.Entries.Single().Message);
    }
}
