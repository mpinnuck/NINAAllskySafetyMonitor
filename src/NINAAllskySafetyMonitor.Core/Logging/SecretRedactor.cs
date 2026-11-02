using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Logging;

/// <summary>Replaces passwords and API keys with *** in any text about to be logged.</summary>
[Requirement("REQ-54")]
public sealed class SecretRedactor
{
    private const int MinimumLength = 4;
    private readonly Func<IEnumerable<string?>> secrets;

    /// <param name="secrets">Read on every call, so changed settings are picked up.</param>
    public SecretRedactor(Func<IEnumerable<string?>> secrets)
    {
        this.secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        IEnumerable<string?> current;
        try
        {
            current = secrets().ToList();
        }
        catch (Exception)
        {
            // If the secrets can't be read, nothing is known to redact; never fail the log write.
            return text;
        }

        foreach (string? secret in current)
        {
            string s = secret?.Trim() ?? string.Empty;
            if (s.Length >= MinimumLength) text = text.Replace(s, "***", StringComparison.Ordinal);
        }

        return text;
    }
}
