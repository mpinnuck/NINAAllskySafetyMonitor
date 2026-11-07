using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>Names of the secrets held in <see cref="ICredentialStore"/>.</summary>
[Requirement("REQ-53")]
public static class CredentialKeys
{
    /// <summary>The Allsky Pi SFTP password, used only when password authentication is selected.</summary>
    public const string SftpPassword = "sftp-password";

    /// <summary>The Weather.com API key used for current station observations.</summary>
    public const string WeatherApiKey = "weather-api-key";
}
