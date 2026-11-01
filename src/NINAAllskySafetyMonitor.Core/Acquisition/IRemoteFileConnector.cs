using NINAAllskySafetyMonitor.Core.Settings;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>Opens a file session on the Allsky Pi. Separated from SSH.NET so the image logic can be tested.</summary>
public interface IRemoteFileConnector
{
    /// <summary>
    /// Connects and authenticates. Throws <see cref="HostKeyMismatchException"/> if the Pi's host key
    /// does not match the pinned fingerprint, and other exceptions for connection or login failures.
    /// </summary>
    IRemoteFileSession Connect(DirectSourceSettings settings, TimeSpan connectTimeout);
}
