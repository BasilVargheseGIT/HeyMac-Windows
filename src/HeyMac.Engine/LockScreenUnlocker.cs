namespace HeyMac.Engine;

/// <summary>
/// While the screen is locked and the display is on, looks for the enrolled face and
/// types the stored password — at most once per lock episode. If the screen is still
/// locked afterwards the password is treated as wrong and auto-typing stops until the
/// user saves it again, so a stale password can't lock the account out.
/// Port of HeyMac's LockScreenUnlocker.
/// </summary>
public sealed class LockScreenUnlocker : IDisposable
{
    private static readonly TimeSpan UnlockConfirmationDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UnlockPollInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan NoMatchCooldown = TimeSpan.FromSeconds(3.5);
    private static readonly TimeSpan NoFaceCooldown = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ScanWindow = TimeSpan.FromSeconds(6);
    private static readonly int MaxScansPerEpisode = 2;

    private readonly IFaceMatcher _matcher;
    private readonly IPasswordTyping _typist;
    private readonly EngineSettings _settings;
    private readonly ILockScreenEnvironment _environment;
    private readonly Action<EngineEvent> _onEvent;

    private readonly object _stateLock = new();
    private bool _running;
    private bool _stopRequested;
    private bool _attemptedThisEpisode;
    private int _scansThisEpisode;
    private string? _lastReportedProblem;

    public LockScreenUnlocker(IFaceMatcher matcher, IPasswordTyping typist, EngineSettings settings,
        ILockScreenEnvironment environment, Action<EngineEvent> onEvent)
    {
        _matcher = matcher;
        _typist = typist;
        _settings = settings;
        _environment = environment;
        _onEvent = onEvent;
    }

    public void Start()
    {
        bool shouldStart;
        lock (_stateLock)
        {
            if (_running) return;
            _running = true;
            _stopRequested = false;
            shouldStart = true;
        }

        var thread = new System.Threading.Thread(Loop) { IsBackground = true };
        thread.Start();
    }

    public void Stop()
    {
        lock (_stateLock) { _stopRequested = true; }
    }

    private void Loop()
    {
        try
        {
            while (true)
            {
                lock (_stateLock)
                {
                    if (_stopRequested) break;
                }
                Tick();
                _environment.Sleep(PollInterval);
            }
        }
        finally
        {
            lock (_stateLock) { _running = false; }
        }
    }

    public LockScreenTick Tick()
    {
        bool? locked = _environment.IsLocked();
        if (locked != true)
        {
            lock (_stateLock)
            {
                _attemptedThisEpisode = false;
                _scansThisEpisode = 0;
                _lastReportedProblem = null;
            }
            return LockScreenTick.NotLocked;
        }

        lock (_stateLock)
        {
            if (_attemptedThisEpisode) return LockScreenTick.AlreadyAttempted;
            if (_scansThisEpisode >= MaxScansPerEpisode)
            {
                if (!_environment.DisplayIsAwake()) _scansThisEpisode = 0;
                return LockScreenTick.RetriesExhausted;
            }
        }

        if (!_environment.DisplayIsAwake()) return LockScreenTick.DisplayAsleep;

        string password;
        try { password = _environment.LoadPassword(); }
        catch { return Report("stored password unavailable"); }

        lock (_stateLock) { _scansThisEpisode++; }
        _onEvent(EngineEvent.LockScreenScanning);

        VerificationOutcome outcome = _matcher.Run(
            timeout: _settings.LockScreenScanTimeout,
            requiredConsecutive: 2,
            waitForTurn: TimeSpan.Zero,
            keepGoing: ShouldKeepScanning);

        if (!outcome.Matched)
        {
            _onEvent(EngineEvent.LockScreenScanEnded);
            if (outcome.Failure != null) return Report(outcome.Failure);
            if (!ShouldKeepScanning()) return LockScreenTick.NoMatch;
            if (outcome.Mismatched)
            {
                _onEvent(EngineEvent.LockScreenNoMatch);
                WaitOutAnimation(NoMatchCooldown);
            }
            else
            {
                _onEvent(EngineEvent.LockScreenNoFace);
                WaitOutAnimation(NoFaceCooldown);
            }
            return LockScreenTick.NoMatch;
        }

        lock (_stateLock) { _attemptedThisEpisode = true; }

        try
        {
            _typist.Type(password);
        }
        catch (Exception ex)
        {
            _onEvent(EngineEvent.LockScreenScanEnded);
            return LockScreenTick.TypingFailed;
        }

        var elapsed = TimeSpan.Zero;
        while (elapsed < UnlockConfirmationDelay)
        {
            _environment.Sleep(UnlockPollInterval);
            elapsed += UnlockPollInterval;
            if (_environment.IsLocked() == false)
            {
                _onEvent(EngineEvent.LockScreenUnlocked);
                return LockScreenTick.Unlocked;
            }
        }

        _settings.LockScreenNeedsPassword = true;
        _onEvent(EngineEvent.LockScreenPasswordRejected);
        return LockScreenTick.PasswordRejected;
    }

    private bool ShouldKeepScanning()
    {
        lock (_stateLock)
        {
            return !_stopRequested && !_settings.Paused && _environment.IsLocked() == true &&
                   _environment.DisplayIsAwake();
        }
    }

    private void WaitOutAnimation(TimeSpan duration)
    {
        TimeSpan waited = TimeSpan.Zero;
        while (waited < duration && ShouldKeepScanning())
        {
            _environment.Sleep(TimeSpan.FromMilliseconds(250));
            waited += TimeSpan.FromMilliseconds(250);
        }
    }

    private LockScreenTick Report(string problem)
    {
        bool isNew;
        lock (_stateLock)
        {
            isNew = _lastReportedProblem != problem;
            _lastReportedProblem = problem;
        }
        if (isNew) _onEvent(EngineEvent.LockScreenProblem(problem));
        return LockScreenTick.Blocked;
    }

    public void Dispose() { }
}
