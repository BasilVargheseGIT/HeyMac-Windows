namespace HeyMac.Core.Security;

/// <summary>
/// AES-GCM encrypted file store. Port of HeyMac's SecureStore:
/// - encrypts with AES-GCM using a key from the key provider
/// - writes atomically to a temp file then renames into place
/// HeyMac protected the key with the macOS Keychain; this Windows version protects it with DPAPI.
/// </summary>
public sealed class SecureStore
{
    private readonly ISymmetricKeyProvider _keyProvider;
    private readonly string _directory;
    public const string CentroidKey = "face-centroid";
    public const string LoginPasswordKey = "login-password";

    public SecureStore(ISymmetricKeyProvider keyProvider, string? directory = null)
    {
        _keyProvider = keyProvider;
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeyMac", "store");
        Directory.CreateDirectory(_directory);
    }

    public void Save(byte[] data, string name)
    {
        byte[] key = _keyProvider.FetchOrCreateKey();
        byte[] combined;
        try
        {
            combined = AesGcm.Seal(data, key);
        }
        catch (Exception ex)
        {
            throw new SecureStoreException(SecureStoreError.EncryptionFailed, "AES-GCM encryption failed", ex);
        }

        string tempPath = Path.Combine(_directory, $".{name}.tmp-{Guid.NewGuid():N}");
        string url = Path.Combine(_directory, name);

        try
        {
            File.WriteAllBytes(tempPath, combined);
            File.Move(tempPath, url, overwrite: true); // atomic replace
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw new SecureStoreException(SecureStoreError.EncryptionFailed, "failed to write encrypted store file");
        }
    }

    public byte[] Load(string name)
    {
        string url = Path.Combine(_directory, name);
        if (!File.Exists(url))
            throw new SecureStoreException(SecureStoreError.NotFound, $"store item not found: {name}");

        byte[] key = _keyProvider.FetchOrCreateKey();
        byte[] combined = File.ReadAllBytes(url);
        try
        {
            return AesGcm.Open(combined, key);
        }
        catch (Exception ex)
        {
            throw new SecureStoreException(SecureStoreError.DecryptionFailed, "AES-GCM decryption failed", ex);
        }
    }
}
