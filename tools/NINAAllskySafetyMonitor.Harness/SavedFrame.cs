namespace NINAAllskySafetyMonitor.Harness;

/// <summary>A saved Allsky frame.</summary>
/// <param name="Path">Full path of the JPEG.</param>
/// <param name="Night">The date the night started (Allsky's folder name).</param>
/// <param name="CaptureUtc">Capture time, from the time in the file name.</param>
public sealed record SavedFrame(string Path, DateOnly Night, DateTime CaptureUtc);
