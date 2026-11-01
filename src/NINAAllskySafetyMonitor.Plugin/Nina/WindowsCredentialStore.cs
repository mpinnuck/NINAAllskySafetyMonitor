using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>
/// Keeps secrets in Windows Credential Manager (Control Panel → Credential Manager → Windows
/// Credentials, entries named "NINAAllskySafetyMonitor/…"), never in the settings file.
/// </summary>
[Requirement("REQ-53")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string Prefix = "NINAAllskySafetyMonitor/";

    /// <inheritdoc />
    public string? GetSecret(string key)
    {
        if (!CredentialNativeMethods.CredRead(Prefix + key, CredentialNativeMethods.CredTypeGeneric, 0, out IntPtr buffer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == CredentialNativeMethods.ErrorNotFound) return null;
            throw new Win32Exception(error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<CredentialNativeMethods.Credential>(buffer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return string.Empty;
            return Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredentialNativeMethods.CredFree(buffer);
        }
    }

    /// <summary>Stores the secret; an empty value removes it.</summary>
    public void SetSecret(string key, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (!CredentialNativeMethods.CredDelete(Prefix + key, CredentialNativeMethods.CredTypeGeneric, 0))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != CredentialNativeMethods.ErrorNotFound) throw new Win32Exception(error);
            }

            return;
        }

        byte[] bytes = Encoding.Unicode.GetBytes(value);
        IntPtr blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new CredentialNativeMethods.Credential
            {
                Type = CredentialNativeMethods.CredTypeGeneric,
                TargetName = Prefix + key,
                CredentialBlobSize = bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialNativeMethods.CredPersistLocalMachine,
                UserName = Environment.UserName,
            };

            if (!CredentialNativeMethods.CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            Array.Clear(bytes);
        }
    }
}
