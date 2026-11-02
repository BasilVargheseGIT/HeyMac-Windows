namespace HeyMac.App.AppLock;

using HeyMac.Core.Security;

/// <summary>
/// Simple in-memory + encrypted store for app-lock configuration, exposed to the UI.
/// </summary>
public sealed class AppLockManager
{
    public AppLockSettings Settings { get; }
    public AppLockController? Controller { get; private set; }

    public AppLockManager(SecureStore store)
    {
        Settings = new AppLockSettings(store);
        Settings.Load();
    }

    public void Start(IntPtr mainWindowHandle, IFaceMatcher verifier)
    {
        Controller?.Dispose();
        Controller = new AppLockController(Settings, verifier, mainWindowHandle);
        Controller.Start();
    }

    public void AddOrUpdate(ProtectedApp app)
    {
        var existing = Settings.Apps.FirstOrDefault(a =>
            a.Identifier == app.Identifier && a.MatchByProcessName == app.MatchByProcessName);
        if (existing != null)
        {
            existing.Mode = app.Mode;
            existing.Policy = app.Policy;
            existing.TimedLockAfter = app.TimedLockAfter;
            existing.Enabled = app.Enabled;
        }
        else
        {
            Settings.Apps.Add(app);
        }
        Settings.Save();
    }

    public void Remove(string identifier, bool byProcessName)
    {
        Settings.Apps.RemoveAll(a =>
            a.Identifier == identifier && a.MatchByProcessName == byProcessName);
        Settings.Save();
    }
}
