namespace HeyMac.Engine;

/// <summary>
/// Events emitted by the engine, for logging and UI. Port of HeyMac's EngineEvent.
/// </summary>
public enum EngineEvent
{
    LockScreenScanning,
    LockScreenScanEnded,
    LockScreenNoMatch,
    LockScreenNoFace,
    LockScreenUnlocked,
    LockScreenPasswordRejected,
    LockScreenProblem,
}

public static class EngineEventExtensions
{
    public static string LogText(this EngineEvent e) => e switch
    {
        EngineEvent.LockScreenScanning => "lock screen: scanning",
        EngineEvent.LockScreenScanEnded => "lock screen: scan ended",
        EngineEvent.LockScreenNoMatch => "lock screen: no match",
        EngineEvent.LockScreenNoFace => "lock screen: no face",
        EngineEvent.LockScreenUnlocked => "lock screen: unlocked",
        EngineEvent.LockScreenPasswordRejected => "lock screen: password rejected",
        EngineEvent.LockScreenProblem => "lock screen: problem",
        _ => e.ToString()
    };
}
