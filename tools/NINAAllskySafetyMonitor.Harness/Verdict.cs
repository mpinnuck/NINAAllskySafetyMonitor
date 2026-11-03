namespace NINAAllskySafetyMonitor.Harness;

/// <summary>The cloud verdict for one target in one frame.</summary>
public enum Verdict
{
    Clear,
    Cloud,

    /// <summary>Cloud could not be judged (too few stars expected and no clear-sky reference).</summary>
    Unknown,
}
