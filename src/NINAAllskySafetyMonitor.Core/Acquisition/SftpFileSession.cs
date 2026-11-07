using System.IO;
using NINAAllskySafetyMonitor.Core.Traceability;
using Renci.SshNet;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>An open SSH.NET SFTP connection to the Pi.</summary>
[Requirement("REQ-11")]
public sealed class SftpFileSession : IRemoteFileSession
{
    private readonly SftpClient client;
    private readonly PrivateKeyFile? privateKeyFile;

    public SftpFileSession(SftpClient client, PrivateKeyFile? privateKeyFile = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.privateKeyFile = privateKeyFile;
    }

    public async Task<DateTime> GetLastWriteTimeUtcAsync(string path, CancellationToken ct)
    {
        var attributes = await client.GetAttributesAsync(path, ct).ConfigureAwait(false);
        return DateTime.SpecifyKind(attributes.LastWriteTimeUtc, DateTimeKind.Utc);
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        await client.DownloadFileAsync(path, stream, ct).ConfigureAwait(false);
        return stream.ToArray();
    }

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

        try
        {
            client.Dispose();
        }
        finally
        {
            privateKeyFile?.Dispose();
        }
    }
}
