namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>Severity of a log entry written through <see cref="ILogSink"/>.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}
