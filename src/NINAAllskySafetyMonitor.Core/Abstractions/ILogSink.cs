namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>Destination for log entries. Implemented by the host (NINA log, console, rolling file).</summary>
public interface ILogSink
{
    /// <summary>Writes one log entry. Implementations must not throw.</summary>
    void Write(LogLevel level, string message);
}
