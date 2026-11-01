using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>An Allsky image as fetched, with its capture time and source.</summary>
/// <param name="JpegBytes">The JPEG file contents.</param>
/// <param name="CaptureTimeUtc">Capture time (file modification time or web server last-modified time).</param>
/// <param name="Source">Where the image came from.</param>
[Requirement("REQ-11", "REQ-15")]
public sealed record SkyImage(byte[] JpegBytes, DateTime CaptureTimeUtc, ImageSourceKind Source);
