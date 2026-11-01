using NINAAllskySafetyMonitor.Core.Abstractions;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Log sink that keeps entries in memory for assertions.</summary>
public sealed class ListLogSink : ILogSink
{
    private readonly List<(LogLevel Level, string Message)> entries = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get { lock (entries) return entries.ToList(); }
    }

    public void Write(LogLevel level, string message)
    {
        lock (entries) entries.Add((level, message));
    }
}
