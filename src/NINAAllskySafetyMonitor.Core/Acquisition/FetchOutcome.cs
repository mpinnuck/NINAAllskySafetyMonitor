namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>Result type of one fetch from an image source.</summary>
public enum FetchOutcome
{
    /// <summary>A new image was downloaded.</summary>
    NewImage,

    /// <summary>The source was reached but the image has not changed since the last fetch.</summary>
    Unchanged,

    /// <summary>The fetch failed; see <see cref="FetchResult.Error"/>.</summary>
    Failed,
}
