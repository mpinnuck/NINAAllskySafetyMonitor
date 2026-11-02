using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>A frame to calibrate from, with the time it was taken.</summary>
/// <param name="Frame">Decoded image (darkest colour channel per pixel).</param>
/// <param name="CaptureUtc">When the frame was taken, UTC.</param>
/// <param name="Source">File name or "current image", for messages.</param>
public sealed record CalibrationFrame(GrayFrame Frame, DateTime CaptureUtc, string Source);
