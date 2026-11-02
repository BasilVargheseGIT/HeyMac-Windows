namespace HeyMac.Engine;

using HeyMac.Core.Security;

/// <summary>
/// Runs the background lock-screen watcher. Port of HeyMac's EngineController.
/// </summary>
public sealed class EngineController : IDisposable
{
    private readonly EngineSettings _settings;
    private readonly Action<EngineEvent> _onEvent;
    private readonly object _stateLock = new();
    private LockScreenUnlocker? _unlocker;

    public EngineController(EngineSettings settings, Action<EngineEvent> onEvent)
    {
        _settings = settings;
        _onEvent = onEvent;
    }

    public bool IsRunning
    {
        get
        {
            lock (_stateLock) { return _unlocker != null; }
        }
    }

    public void Start(ILockScreenEnvironment environment, IFaceMatcher matcher, IPasswordTyping typist)
    {
        Stop();
        if (!_settings.SetupComplete)
        {
            WriteLog("engine idle: setup is not complete");
            return;
        }

        _unlocker = new LockScreenUnlocker(matcher, typist, _settings, environment, _onEvent);
        _unlocker.Start();
        WriteLog($"engine started (strictness {_settings.Strictness})");
    }

    public void Stop()
    {
        LockScreenUnlocker? unlocker;
        lock (_stateLock)
        {
            unlocker = _unlocker;
            _unlocker = null;
        }
        unlocker?.Stop();
        if (unlocker != null) WriteLog("engine stopped");
    }

    private void WriteLog(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[HeyMac] {message}");
        _onEvent(EngineEvent.LockScreenScanning); // surface to UI if desired
    }

    public void Dispose() => Stop();
}
