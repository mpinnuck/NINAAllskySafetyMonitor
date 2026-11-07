using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Logging;

/// <summary>Replaces passwords and API keys with *** in any text about to be logged.</summary>
[Requirement("REQ-54")]
public sealed class SecretRedactor
{
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
            return "[redaction unavailable; message suppressed]";
        }

        foreach (string? secret in current)
        {
            string s = secret?.Trim() ?? string.Empty;
            if (s.Length > 0) text = text.Replace(s, "***", StringComparison.Ordinal);
        }

        return text;
    }
}
