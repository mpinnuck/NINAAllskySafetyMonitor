using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Image acquisition settings.</summary>
[Requirement("REQ-11", "REQ-14", "REQ-15", "REQ-17", "REQ-18", "REQ-51")]
public sealed class ImageSourceSettings
{
    /// <summary>Selected source: Web or Direct.</summary>
    public ImageSourceKind Kind { get; set; } = ImageSourceKind.Direct;

    /// <summary>Seconds between image fetches while active.</summary>
    public int IntervalS { get; set; } = 60;

    /// <summary>An image older than this (minutes) is stale and unsafe.</summary>
    public int StaleAfterMin { get; set; } = 10;

    /// <summary>No successful fetch for this many minutes is unsafe.</summary>
    public int FetchFailUnsafeMin { get; set; } = 10;

    /// <summary>Seconds to wait before refetching an incomplete JPEG.</summary>
    public int IncompleteRetryS { get; set; } = 5;

    /// <summary>Attempts allowed for an incomplete JPEG.</summary>
    public int IncompleteRetries { get; set; } = 3;

    /// <summary>Timeout for one fetch, in seconds.</summary>
    public int TimeoutS { get; set; } = 20;

    public WebSourceSettings Web { get; set; } = new();
    public DirectSourceSettings Direct { get; set; } = new();
}
