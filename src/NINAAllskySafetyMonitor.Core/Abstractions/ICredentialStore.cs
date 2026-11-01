using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>
/// Secure storage for secrets that must never be written to the settings file
/// (Windows Credential Manager in the plugin).
/// </summary>
[Requirement("REQ-53")]
public interface ICredentialStore
{
    /// <summary>Returns the secret stored under <paramref name="key"/>, or null if none.</summary>
    string? GetSecret(string key);

    /// <summary>Stores or replaces the secret under <paramref name="key"/>.</summary>
    void SetSecret(string key, string value);
}
