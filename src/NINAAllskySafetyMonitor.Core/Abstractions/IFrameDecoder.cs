using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Abstractions;

/// <summary>
/// Decodes an Allsky JPEG into a <see cref="GrayFrame"/> holding, for each pixel, the darkest of its
/// red, green and blue values. Stars are white, so they keep their brightness; the sensor's noisy
/// pixels are strongly coloured (mostly red), so they are suppressed. Implemented by the plugin over
/// WPF, which keeps Core free of an imaging library.
/// </summary>
[Requirement("REQ-20")]
public interface IFrameDecoder
{
    /// <summary>Decodes the JPEG. Throws when it is not a readable image.</summary>
    GrayFrame Decode(byte[] jpeg);
}
