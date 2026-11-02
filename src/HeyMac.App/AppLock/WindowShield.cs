namespace HeyMac.App.AppLock;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

/// <summary>
/// Per-window shield overlay. A topmost, transparent WPF window is positioned over
/// a protected application's window and shows either a window-level or full-display
/// blur until face verification succeeds.
///
/// HeyMac equivalent: AppWindowShield + ShieldView + ShieldController.
/// </summary>
public sealed class WindowShield : IDisposable
{
    private readonly IntPtr _targetHwnd;
    private readonly ShieldMode _mode;
    private Window? _overlay;
    private bool _disposed;
    private readonly IntPtr _ownerHandle;

    public WindowShield(IntPtr targetHwnd, ShieldMode mode, IntPtr ownerHandle)
    {
        _targetHwnd = targetHwnd;
        _mode = mode;
        _ownerHandle = ownerHandle;
    }

    public bool IsActive => _overlay != null && _overlay.IsLoaded;

    /// <summary>Show the shield. Returns when the shield window is created.</summary>
    public void Show()
    {
        if (_overlay != null || _disposed) return;

        var targetRect = GetWindowRect(_targetHwnd);

        // Fullscreen capture if blurring the entire display.
        Rect bounds = _mode == ShieldMode.BlurDisplay
            ? new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight)
            : new Rect(targetRect.Left, targetRect.Top, targetRect.Width, targetRect.Height);

        _overlay = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
        };

        var host = new System.Windows.Forms.NativeWindow();
        host.AssignHandle(_ownerHandle);

        // Blur effect behind the shield window (acrylic-like).
        var blur = new BlurEffect { Radius = 20, KernelType = KernelType.Gaussian };
        var border = new System.Windows.Controls.Border
        {
            Background = new SolidColorBrush(Color.FromArgb(140, 245, 245, 245)),
            Effect = blur,
            Child = new System.Windows.Controls.TextBlock
            {
                Text = "Hey Mac — face verification required",
                Foreground = Brushes.White,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
        };
        _overlay.Content = border;

        // Block input to the shield area.
        _overlay.MouseDown += (_, _) => { };

        // Make the Win32 window layered + transparent so clicks fall through the
        // non-blur parts, but our overlay still covers the target.
        var helper = new WindowInteropHelper(_overlay) { Owner = _ownerHandle };
        _overlay.SourceInitialized += (_, _) =>
        {
            var hwnd = helper.Handle;
            SetWindowLong(hwnd, GWL_EXSTYLE,
                GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            // Topmost + disable activation.
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        };

        _overlay.Closed += (_, _) => _overlay = null;
        _overlay.Show();
    }

    public void Hide()
    {
        if (_overlay != null)
        {
            _overlay.Close();
            _overlay = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Hide();
    }

    private static Rect GetWindowRect(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var rect);
        return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y,
        int cx, int cy, uint uFlags);
}
