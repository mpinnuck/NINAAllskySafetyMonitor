using NINAAllskySafetyMonitor.Core.Traceability;
using Renci.SshNet;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>An open SSH.NET SFTP connection to the Pi.</summary>
[Requirement("REQ-11")]
public sealed class SftpFileSession : IRemoteFileSession
{
    private readonly SftpClient client;

    public SftpFileSession(SftpClient client)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public DateTime GetLastWriteTimeUtc(string path) =>
        DateTime.SpecifyKind(client.GetLastWriteTimeUtc(path), DateTimeKind.Utc);

    public byte[] ReadAllBytes(string path) => client.ReadAllBytes(path);

    public void Dispose()
    {
        try
        {
            if (client.IsConnected) client.Disconnect();
        }
        catch (Exception)
        {
            // Disconnect errors don't matter: the session is finished either way.
        }

        client.Dispose();
    }
}
