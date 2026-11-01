using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>What one fetch from an image source produced.</summary>
/// <param name="Outcome">New image, unchanged or failed.</param>
/// <param name="Image">The new image when <paramref name="Outcome"/> is <see cref="FetchOutcome.NewImage"/>.</param>
/// <param name="Error">Readable reason when <paramref name="Outcome"/> is <see cref="FetchOutcome.Failed"/>.</param>
/// <param name="Untrusted">True when the source could not be trusted (host key mismatch): earlier images must not be used either.</param>
[Requirement("REQ-16", "REQ-52")]
public sealed record FetchResult(FetchOutcome Outcome, SkyImage? Image, string? Error, bool Untrusted = false)
{
    public static FetchResult New(SkyImage image) => new(FetchOutcome.NewImage, image, null);

    public static FetchResult Unchanged() => new(FetchOutcome.Unchanged, null, null);

    public static FetchResult Failed(string error) => new(FetchOutcome.Failed, null, error);

    public static FetchResult FailedUntrusted(string error) => new(FetchOutcome.Failed, null, error, true);
}
