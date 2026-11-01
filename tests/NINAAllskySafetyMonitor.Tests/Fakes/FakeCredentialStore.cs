using NINAAllskySafetyMonitor.Core.Abstractions;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>In-memory credential store.</summary>
public sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> secrets = new();

    public string? GetSecret(string key) => secrets.TryGetValue(key, out string? v) ? v : null;

    public void SetSecret(string key, string value) => secrets[key] = value;
}
