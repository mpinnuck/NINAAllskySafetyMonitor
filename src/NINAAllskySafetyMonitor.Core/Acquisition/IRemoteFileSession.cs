namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>An open connection to the Allsky Pi's file system. Disposing it disconnects.</summary>
public interface IRemoteFileSession : IDisposable
{
    /// <summary>Modification time of a remote file, in UTC.</summary>
    DateTime GetLastWriteTimeUtc(string path);

    /// <summary>Downloads a remote file.</summary>
    byte[] ReadAllBytes(string path);
}
