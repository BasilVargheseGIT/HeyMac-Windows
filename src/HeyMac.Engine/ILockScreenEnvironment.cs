namespace HeyMac.Engine;

/// <summary>
/// Everything the unlocker needs from the system, injectable for tests.
/// Port of HeyMac's LockScreenEnvironment.
/// </summary>
public interface ILockScreenEnvironment
{
    bool? IsLocked();
    bool DisplayIsAwake();
    string LoadPassword();
    void WakeDisplay();
    void Sleep(TimeSpan interval);
}

public interface IPasswordTyping
{
    void Type(string password);
}

public enum LockScreenTick
{
    NotLocked,
    AlreadyAttempted,
    Blocked,
    DisplayAsleep,
    NoMatch,
    TypingFailed,
    Unlocked,
    PasswordRejected,
    RetriesExhausted,
    NotEnrolled,
    KeychainInteractionRequired,
}
