using NINA.Core.Utility;
using NINAAllskySafetyMonitor.Core.Abstractions;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>Writes Core log entries to the NINA log, prefixed with the plugin name.</summary>
public sealed class NinaLogSink : ILogSink
{
    private const string Prefix = "[AllskySafetyMonitor] ";

    /// <inheritdoc />
    public void Write(LogLevel level, string message)
    {
        try
        {
            switch (level)
            {
                case LogLevel.Error:
                    Logger.Error(Prefix + message);
                    break;
                case LogLevel.Warning:
                    Logger.Warning(Prefix + message);
                    break;
                case LogLevel.Info:
                    Logger.Info(Prefix + message);
                    break;
                default:
                    Logger.Debug(Prefix + message);
                    break;
            }
        }
        catch
        {
            // Logging must never take the monitor down.
        }
    }
}
