namespace HeyMac.App.AppLock;

using HeyMac.Core.Security;

/// <summary>
/// Persists the list of protected apps and per-app settings.
/// Settings are stored encrypted with AES-GCM (same SecureStore as the face centroid),
/// so an attacker can't read the protected-app list without unlocking.
/// </summary>
public sealed class AppLockSettings
{
    private readonly SecureStore _store;
    public const string SettingsKey = "applock-settings";

    public List<ProtectedApp> Apps { get; set; } = new();

    public AppLockSettings(SecureStore store) => _store = store;

    public void Save()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Apps);
        _store.Save(System.Text.Encoding.UTF8.GetBytes(json), SettingsKey);
    }

    public bool Load()
    {
        try
        {
            var data = _store.Load(SettingsKey);
            var json = System.Text.Encoding.UTF8.GetString(data);
            Apps = System.Text.Json.JsonSerializer.Deserialize<List<ProtectedApp>>(json) ?? new();
            return true;
        }
        catch
        {
            Apps = new();
            return false;
        }
    }
}
