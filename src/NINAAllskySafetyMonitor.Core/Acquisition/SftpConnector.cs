using System.Net.Sockets;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>
/// Connects to the Allsky Pi over SFTP: tries the host name, then the fallback IP if the name can't
/// be reached; verifies the host key against the pinned fingerprint before logging in; logs in with
/// the SSH key, or with the password from the credential store.
/// </summary>
[Requirement("REQ-12", "REQ-13", "REQ-51", "REQ-52", "REQ-53")]
public sealed class SftpConnector : IRemoteFileConnector
{
    private readonly ICredentialStore credentials;
    private readonly ILogSink log;

    public SftpConnector(ICredentialStore credentials, ILogSink log)
    {
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public IRemoteFileSession Connect(DirectSourceSettings settings, TimeSpan connectTimeout)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var hosts = new List<string> { settings.Host.Trim() };
        string fallback = settings.FallbackIp?.Trim() ?? string.Empty;
        if (fallback.Length > 0 && !string.Equals(fallback, hosts[0], StringComparison.OrdinalIgnoreCase))
        {
            hosts.Add(fallback);
        }

        AuthenticationMethod auth = CreateAuthentication(settings);
        var verifier = new HostKeyVerifier(settings.HostKeyFingerprint);
        Exception? last = null;

        foreach (string host in hosts)
        {
            try
            {
                return ConnectTo(host, settings, auth, verifier, connectTimeout);
            }
            catch (Exception e) when (IsUnreachable(e))
            {
                last = e;
                if (host != hosts[^1])
                {
                    log.Write(LogLevel.Info, $"Could not reach {host} ({e.Message}); trying {hosts[^1]}");
                }
            }
        }

        throw new IOException($"Could not connect to {string.Join(" or ", hosts)}: {last?.Message}", last);
    }

    private AuthenticationMethod CreateAuthentication(DirectSourceSettings s)
    {
        if (s.UsePassword)
        {
            string password = credentials.GetSecret(CredentialKeys.SftpPassword)
                ?? throw new InvalidOperationException("Password login is selected but no SFTP password is stored. Enter it on the settings page.");
            return new PasswordAuthenticationMethod(s.User, password);
        }

        if (!File.Exists(s.KeyFile))
        {
            throw new FileNotFoundException($"SSH key file not found: {s.KeyFile}");
        }

        return new PrivateKeyAuthenticationMethod(s.User, new PrivateKeyFile(s.KeyFile));
    }

    private static IRemoteFileSession ConnectTo(string host, DirectSourceSettings s, AuthenticationMethod auth, HostKeyVerifier verifier, TimeSpan timeout)
    {
        var info = new ConnectionInfo(host, s.Port, s.User, auth) { Timeout = timeout };
        var client = new SftpClient(info) { OperationTimeout = timeout };

        string? received = null;
        bool mismatch = false;
        client.HostKeyReceived += (_, e) =>
        {
            received = $"{e.HostKeyName} SHA256:{e.FingerPrintSHA256}";
            e.CanTrust = verifier.Verify(e.FingerPrintSHA256);
            mismatch = !e.CanTrust;
        };

        try
        {
            client.Connect();
            return new SftpFileSession(client);
        }
        catch (Exception) when (mismatch)
        {
            client.Dispose();
            throw new HostKeyMismatchException(host, received ?? "unknown key");
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>Errors that mean "this address can't be reached", so the fallback is worth trying.</summary>
    private static bool IsUnreachable(Exception e) =>
        e is SocketException or SshOperationTimeoutException or SshConnectionException or TimeoutException;
}
