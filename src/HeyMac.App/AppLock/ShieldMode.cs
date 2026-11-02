namespace HeyMac.App.AppLock;

/// <summary>
/// How strictly a protected application is shielded.
/// Mirrors HeyMac's ShieldMode (blur whole display vs blur app window only).
/// </summary>
public enum ShieldMode
{
    None,            // app not protected
    BlurWindow,      // blur only the locked application window
    BlurDisplay,     // blur the entire display (maximum privacy)
}

public enum RelockPolicy
{
    Immediate,       // require auth every time the app gains focus
    Timed,           // require auth after N minutes since last unlock
    OnFocusLoss,     // require auth when focus leaves then returns
}

public sealed class ProtectedApp
{
    /// <summary>Process name (e.g. "whatsapp") or window title substring to match.</summary>
    public string Identifier { get; set; } = "";
    /// <summary>true = match by process name; false = match by window title substring.</summary>
    public bool MatchByProcessName { get; set; } = true;
    public ShieldMode Mode { get; set; } = ShieldMode.BlurWindow;
    public RelockPolicy Policy { get; set; } = RelockPolicy.Immediate;
    public TimeSpan TimedLockAfter { get; set; } = TimeSpan.FromMinutes(10);
    public bool Enabled { get; set; } = true;
}
