namespace HeyMac.Engine.Win32;

using System.Runtime.InteropServices;

/// <summary>
/// Detects whether the Windows workstation is locked and can wake the display.
///
/// Primary technique: OpenInputDesktop() returns NULL when the secure desktop
/// (lock screen) is active, which works without a UI message pump. Supplemented
/// by SystemEvents.SessionSwitch (SessionLock/SessionUnlock) for fast response
/// when a message pump is available.
/// </summary>
public sealed class LockScreenDetector : IDisposable
{
    private const uint DESKTOP_READOBJECTS = 0x0001;
    private const uint DESKTOP_WRITEOBJECTS = 0x0002;
    private const uint DF_ALLOWOTHERACCOUNTHOOK = 0x0001;

    private bool _lastKnownLocked;
    private bool _sessionSwitchHooked;

    public bool IsLocked()
    {
        IntPtr hDesk = OpenInputDesktop(0, false, DESKTOP_READOBJECTS | DESKTOP_WRITEOBJECTS);
        bool locked = hDesk == IntPtr.Zero;
        if (hDesk != IntPtr.Zero) CloseDesktop(hDesk);
        _lastKnownLocked = locked;
        return locked;
    }

    /// <summary>Wake the display (broadcast monitor-power-on).</summary>
    public void WakeDisplay()
    {
        const int HWND_BROADCAST = 0xFFFF;
        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MONITORPOWER = 0xF170;
        SendMessage(new IntPtr(HWND_BROADCAST), WM_SYSCOMMAND, SC_MONITORPOWER, -1);
    }

    public void Dispose() { }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    private static IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam) =>
        SendMessage(hWnd, Msg, (IntPtr)wParam, (IntPtr)lParam);
}
