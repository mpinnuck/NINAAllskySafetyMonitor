using NINA.Core.Utility;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Logging;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>Writes Core log entries to the NINA log, prefixed with the plugin name.</summary>
public sealed class NinaLogSink : ILogSink
{
    private const string Prefix = "[AllskySafetyMonitor] ";
    private static string?[]? cachedSecrets;
    private static readonly SecretRedactor Redactor = new(() =>
    {
        return Volatile.Read(ref cachedSecrets)
            ?? throw new InvalidOperationException("Credential secrets are unavailable for log redaction.");
    });

    static NinaLogSink() => RefreshSecrets();

    /// <summary>Refreshes the redaction cache after credentials change.</summary>
    public static void RefreshSecrets()
    {
        try
        {
            var credentials = new WindowsCredentialStore();
            Volatile.Write(ref cachedSecrets, new[]
            {
                credentials.GetSecret(CredentialKeys.SftpPassword),
                credentials.GetSecret(CredentialKeys.WeatherApiKey),
            });
        }
        catch
        {
            Volatile.Write(ref cachedSecrets, null);
        }
    }

    /// <inheritdoc />
    public void Write(LogLevel level, string message)
    {
        try
        {
            message = Redactor.Redact(message);
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
