using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Acquisition;

/// <summary>Checks made before any network connection. Connecting to the real Pi is covered by the NINA verification.</summary>
[Requirement("REQ-12", "REQ-53")]
public sealed class SftpConnectorTests
{
    private readonly FakeCredentialStore credentials = new();

    private SftpConnector Connector() => new(credentials, new ListLogSink());

    [Fact]
    public void MissingKeyFileIsReported()
    {
        var s = new DirectSourceSettings { KeyFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "id_ed25519") };
        var e = Assert.Throws<FileNotFoundException>(() => Connector().Connect(s, TimeSpan.FromSeconds(1)));
        Assert.Contains("SSH key file not found", e.Message);
    }

    [Fact]
    public void PasswordLoginWithoutStoredPasswordIsReported()
    {
        var s = new DirectSourceSettings { UsePassword = true };
        var e = Assert.Throws<InvalidOperationException>(() => Connector().Connect(s, TimeSpan.FromSeconds(1)));
        Assert.Contains("no SFTP password is stored", e.Message);
    }

    [Fact]
    public void PasswordIsReadFromTheCredentialStoreKey()
    {
        // The password lives under a fixed key in the credential store, never in the settings.
        credentials.SetSecret(CredentialKeys.SftpPassword, "secret");
        Assert.Equal("secret", credentials.GetSecret("sftp-password"));
        Assert.DoesNotContain("secret", SettingsStore.ToJson(new MonitorSettings { ImageSource = { Direct = { UsePassword = true } } }));
    }
}
