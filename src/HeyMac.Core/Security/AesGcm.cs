using System.Security.Cryptography;

namespace HeyMac.Core.Security;

/// <summary>
/// AES-GCM seal/open helper. HeyMac used CryptoKit AES.GCM; this is the .NET equivalent
/// (AesGcm, available in .NET 6+), producing a sealed box = nonce || ciphertext || tag.
/// </summary>
public static class AesGcm
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>Encrypts `plaintext` with a 256-bit key, returns nonce||ciphertext||tag.</summary>
    public static byte[] Seal(byte[] plaintext, byte[] key)
    {
        using var aes = new System.Security.Cryptography.AesGcm(key);
        byte[] nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        byte[] combined = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, combined, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, NonceSize + ciphertext.Length, TagSize);
        return combined;
    }

    /// <summary>Decrypts a sealed box (nonce||ciphertext||tag) with a 256-bit key.</summary>
    public static byte[] Open(byte[] combined, byte[] key)
    {
        if (combined.Length < NonceSize + TagSize)
            throw new CryptographicException("combined buffer too short");
        int ciphertextLen = combined.Length - NonceSize - TagSize;

        byte[] nonce = new byte[NonceSize];
        byte[] ciphertext = new byte[ciphertextLen];
        byte[] tag = new byte[TagSize];
        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(combined, NonceSize, ciphertext, 0, ciphertextLen);
        Buffer.BlockCopy(combined, NonceSize + ciphertextLen, tag, 0, TagSize);

        using var aes = new System.Security.Cryptography.AesGcm(key);
        byte[] plaintext = new byte[ciphertextLen];
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
