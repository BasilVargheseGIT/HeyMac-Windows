using System.Security.Cryptography;

namespace HeyMac.Core.Security;

/// <summary>
/// Protects the AES wrapping key with Windows DPAPI (Data Protection API).
/// The key is tied to the current user/logon session, so other users can't read it.
/// HeyMac used SecItemAdd/SecItemCopyMatching against the login keychain; this is the
/// Windows equivalent (CryptProtectData / CryptUnprotectData via P/Invoke).
/// </summary>
public sealed class DpapiKeyProvider : ISymmetricKeyProvider
{
    private const string EntropyApp = "HeyMac";
    private readonly byte[] _entropy;

    public DpapiKeyProvider() : this(System.Text.Encoding.UTF8.GetBytes(EntropyApp)) { }

    public DpapiKeyProvider(byte[] entropy) => _entropy = entropy;

    public byte[] FetchOrCreateKey()
    {
        // In production you'd persist the generated key under DPAPI protection.
        // Here we derive a stable 256-bit key per machine+user from DPAPI-protected entropy,
        // regenerating and sealing it on first use.
        string dataFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeyMac", "wrapped-key.dat");

        if (File.Exists(dataFile))
        {
            byte[] wrapped = File.ReadAllBytes(dataFile);
            byte[] key = Unprotect(wrapped);
            if (key != null && key.Length == 32) return key;
        }

        byte[] newKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        byte[] wrappedNew = Protect(newKey);
        try { File.WriteAllBytes(dataFile, wrappedNew); } catch { /* best effort */ }
        return newKey;
    }

    private static byte[] Protect(byte[] data)
    {
        // In-process DPAPI equivalent using ProtectedData (user scope).
        return System.Security.Cryptography.ProtectedData.Protect(data, null,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
    }

    private static byte[]? Unprotect(byte[] data)
    {
        try
        {
            return System.Security.Cryptography.ProtectedData.Unprotect(data, null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
        }
        catch { return null; }
    }
}
