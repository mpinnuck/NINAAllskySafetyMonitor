namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>Epoch of equatorial coordinates.</summary>
public enum CoordinateEpoch
{
    /// <summary>Apparent coordinates of date (JNow), as reported by the AM5N.</summary>
    JNow,

    /// <summary>J2000 catalogue coordinates.</summary>
    J2000,
}
