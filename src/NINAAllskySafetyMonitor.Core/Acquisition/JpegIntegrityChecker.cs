using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>Detects an image caught part-way through being written: a JPEG must start with FFD8 and end with FFD9.</summary>
[Requirement("REQ-17")]
public sealed class JpegIntegrityChecker
{
    /// <summary>True when the bytes are a whole JPEG file (trailing zero padding is allowed).</summary>
    public bool IsComplete(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 4) return false;
        if (bytes[0] != 0xFF || bytes[1] != 0xD8) return false;

        int end = bytes.Length - 1;
        while (end > 1 && bytes[end] == 0x00) end--;
        return bytes[end - 1] == 0xFF && bytes[end] == 0xD9;
    }
}
