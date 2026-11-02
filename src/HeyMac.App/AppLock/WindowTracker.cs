namespace HeyMac.App.AppLock;

using System.Runtime.InteropServices;

/// <summary>
/// Win32 window enumeration + focus tracking for app-lock.
/// Detects when a protected application's window is created, focused, or destroyed.
/// </summary>
public sealed class WindowTracker : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TOPMOST = 0x00000008;

    public delegate void WindowEvent(IntPtr hwnd, string processName, string windowTitle, bool isForeground);

    private readonly WindowEvent _onEvent;
    private WinEventDelegate? _delegate; // keep alive
    private IntPtr _hook;
    private bool _disposed;

    public WindowTracker(WindowEvent onEvent)
    {
        _onEvent = onEvent;
        _delegate = OnWinEvent;
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
            _delegate, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != IntPtr.Zero) UnhookWinEvent(_hook);
    }

    /// <summary>Enumerate all top-level windows and report matches.</summary>
    public void ScanAll(Action<IntPtr, string, string> callback)
    {
        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            int pid;
            GetWindowThreadProcessId(hwnd, out pid);
            try { var p = System.Diagnostics.Process.GetProcessById(pid); }
            catch { return true; }

            var processName = System.IO.Path.GetFileNameWithoutExtension(
                GetModuleFileNameEx(pid) ?? string.Empty);
            var title = GetWindowText(hwnd);
            callback(hwnd, processName, title);
            return true;
        }, IntPtr.Zero);
    }

    public (string ProcessName, string WindowTitle) GetWindowInfo(IntPtr hwnd)
    {
        int pid;
        GetWindowThreadProcessId(hwnd, out pid);
        string processName = "";
        try
        {
            var p = System.Diagnostics.Process.GetProcessById(pid);
            processName = System.IO.Path.GetFileNameWithoutExtension(p.MainModule?.FileName ?? "");
        }
        catch { }
        return (processName, GetWindowText(hwnd));
    }

    private static string GetModuleFileNameEx(int pid)
    {
        try
        {
            var p = System.Diagnostics.Process.GetProcessById(pid);
            return p.MainModule?.FileName;
        }
        catch { return ""; }
    }

    private static string GetWindowText(IntPtr hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len == 0) return "";
        var buf = new char[len + 1];
        GetWindowText(hwnd, buf, buf.Length);
        return new string(buf, 0, len);
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;
        var (proc, title) = GetWindowInfo(hwnd);
        _onEvent(hwnd, proc, title, IsForegroundWindow(hwnd));
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
}
