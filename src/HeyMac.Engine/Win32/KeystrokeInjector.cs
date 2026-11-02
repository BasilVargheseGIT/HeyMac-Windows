namespace HeyMac.Engine.Win32;

using System.Runtime.InteropServices;

/// <summary>
/// Types text into the active input desktop via Win32 SendInput.
///
/// Port of HeyMac's KeystrokeInjector (macOS CGEvent keyboard posting).
///
/// NOTE: To reach the Windows lock screen (secure desktop), the process must be
/// running in the Winlogon desktop context or with sufficient privilege. A plain
/// user-mode app will type into its own desktop; for the lock screen, deploy this
/// via a scheduled task / service configured to "interact with the logon desktop"
/// or as a credential provider. See PORT_GUIDE.md.
/// </summary>
public sealed class KeystrokeInjector : IDisposable
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    public void Type(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        List<INPUT> inputs = new();
        foreach (char c in text)
        {
            // Press key down (unicode)
            inputs.Add(MakeInput(c, false));
            // Key up
            inputs.Add(MakeInput(c, true));
        }

        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
    }

    private static INPUT MakeInput(char c, bool keyUp)
    {
        uint flags = KEYEVENTF_UNICODE;
        if (keyUp) flags |= KEYEVENTF_KEYUP;
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTINPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)c,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    public void Dispose() { }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTINPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTINPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
