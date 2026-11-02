namespace HeyMac.Core.Security;

/// <summary>
/// Errors from the secure store. Mirrors HeyMac's SecureStoreError.
/// </summary>
public enum SecureStoreError
{
    EncryptionFailed,
    DecryptionFailed,
    NotFound
}

public sealed class SecureStoreException : Exception
{
    public SecureStoreError Code { get; }
    public SecureStoreException(SecureStoreError code, string? message = null, Exception? inner = null)
        : base(message ?? code.ToString(), inner) => Code = code;
}

/// <summary>
/// Symmetric key provider abstraction. HeyMac stored the AES key in macOS Keychain;
/// on Windows we protect it with DPAPI (user/machine scope).
/// </summary>
public interface ISymmetricKeyProvider
{
    byte[] FetchOrCreateKey();
}
