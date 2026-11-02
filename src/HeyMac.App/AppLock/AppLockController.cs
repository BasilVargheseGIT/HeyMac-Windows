namespace HeyMac.App.AppLock;

using System.Threading.Tasks;

using HeyMac.Engine;

/// <summary>
/// Coordinates app-lock: watches windows, shows/hides shields, and gates shield
/// dismissal on face verification via the HeyMac engine.
///
/// HeyMac equivalent: AppLockController + ShieldController + AppWatcher + AuthCoordinator.
/// </summary>
public sealed class AppLockController : IDisposable
{
    private readonly AppLockSettings _settings;
    private readonly IFaceMatcher _verifier;
    private readonly WindowTracker _tracker;
    private readonly Dictionary<IntPtr, WindowShield> _shields = new();
    private readonly Dictionary<IntPtr, DateTime> _unlockTimes = new();
    private IntPtr _mainWindowHandle;
    private bool _disposed;

    public AppLockController(AppLockSettings settings, IFaceMatcher verifier, IntPtr mainWindowHandle)
    {
        _settings = settings;
        _verifier = verifier;
        _mainWindowHandle = mainWindowHandle;
        _tracker = new WindowTracker(OnWindowEvent);
    }

    public void Start() => _tracker.ScanAll(RegisterIfProtected);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var s in _shields.Values) s.Dispose();
        _shields.Clear();
        _tracker.Dispose();
    }

    private void OnWindowEvent(IntPtr hwnd, string processName, string windowTitle, bool isForeground)
    {
        // UI-thread marshal is expected from the caller (WPF).
        if (isForeground)
        {
            var app = FindProtectedApp(processName, windowTitle);
            if (app != null && app.Enabled)
            {
                if (!ShouldSkip(hwnd, app))
                    ShowShield(hwnd, app);
                else
                    HideShield(hwnd);
            }
            else
            {
                // Focus moved away from a protected app; could start relock timer.
                HandleFocusLost(hwnd);
            }
        }
    }

    private void RegisterIfProtected(IntPtr hwnd, string processName, string windowTitle)
    {
        if (FindProtectedApp(processName, windowTitle) is { } app && app.Enabled)
            ShowShield(hwnd, app);
    }

    private ProtectedApp? FindProtectedApp(string processName, string windowTitle)
    {
        foreach (var app in _settings.Apps)
        {
            if (!app.Enabled) continue;
            if (app.MatchByProcessName)
            {
                if (string.Equals(processName, app.Identifier, StringComparison.OrdinalIgnoreCase))
                    return app;
            }
            else
            {
                if (windowTitle.IndexOf(app.Identifier, StringComparison.OrdinalIgnoreCase) >= 0)
                    return app;
            }
        }
        return null;
    }

    private bool ShouldSkip(IntPtr hwnd, ProtectedApp app)
    {
        switch (app.Policy)
        {
            case RelockPolicy.Immediate:
                return false; // always shield
            case RelockPolicy.Timed:
                return _unlockTimes.TryGetValue(hwnd, out var t) &&
                       DateTime.UtcNow - t < app.TimedLockAfter;
            case RelockPolicy.OnFocusLoss:
                return false;
            default:
                return false;
        }
    }

    private void HandleFocusLost(IntPtr hwnd)
    {
        if (_shields.ContainsKey(hwnd))
        {
            // Keep shield up but mark for re-auth on next focus if policy says so.
        }
    }

    private void ShowShield(IntPtr hwnd, ProtectedApp app)
    {
        if (_shields.ContainsKey(hwnd)) return;

        var shield = new WindowShield(hwnd, app.Mode, _mainWindowHandle);
        _shields[hwnd] = shield;
        shield.Show();

        // Fire-and-forget: verify face, then dismiss.
        _ = Task.Run(async () =>
        {
            var outcome = _verifier.Run(TimeSpan.FromSeconds(10), requiredConsecutive: 2);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (outcome.Matched)
                {
                    _unlockTimes[hwnd] = DateTime.UtcNow;
                    HideShield(hwnd);
                }
                else
                {
                    // Re-shield on next focus if verification failed.
                }
            });
        });
    }

    private void HideShield(IntPtr hwnd)
    {
        if (_shields.TryGetValue(hwnd, out var shield))
        {
            shield.Dispose();
            _shields.Remove(hwnd);
        }
    }
}
