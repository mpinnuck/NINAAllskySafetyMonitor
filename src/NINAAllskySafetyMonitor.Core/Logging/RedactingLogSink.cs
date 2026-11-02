using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Logging;

/// <summary>Log sink that removes secrets before passing messages on.</summary>
[Requirement("REQ-54")]
public sealed class RedactingLogSink : ILogSink
{
    private readonly ILogSink inner;
    private readonly SecretRedactor redactor;

    public RedactingLogSink(ILogSink inner, SecretRedactor redactor)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
    }

    public void Write(LogLevel level, string message) => inner.Write(level, redactor.Redact(message));
}
