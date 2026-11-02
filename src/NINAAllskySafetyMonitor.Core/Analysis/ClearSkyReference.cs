using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Map of sky brightness and non-uniformity over the working area on a clear, moonless night, captured
/// automatically on the first tuned clear night and stored with the settings.
/// </summary>
/// <param name="Blocks">The block map.</param>
/// <param name="CapturedUtc">When it was captured.</param>
[Requirement("REQ-30", "REQ-57")]
public sealed record ClearSkyReference(BlockStatistics Blocks, DateTime CapturedUtc);
