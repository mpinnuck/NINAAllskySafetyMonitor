using NINAAllskySafetyMonitor.Core.Abstractions;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>Writes log messages to the console.</summary>
public sealed class ConsoleLogSink : ILogSink
{
    public void Write(LogLevel level, string message) =>
        (level >= LogLevel.Warning ? Console.Error : Console.Out).WriteLine($"{level}: {message}");
}
