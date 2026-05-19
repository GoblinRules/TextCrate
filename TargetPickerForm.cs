using System.Runtime.InteropServices;

namespace TextCrate;

internal sealed class TargetPickerForm : Form
{
    private const int WhMouseLl = 14;
    private const int WhKeyboardLl = 13;
    private const int WmLButtonUp = 0x0202;
    private const int WmKeyDown = 0x0100;
    private const int VkEscape = 0x1B;

    private readonly NativeHookProc _mouseProc;
    private readonly NativeHookProc _keyboardProc;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private bool _completed;

    public Point? TargetPoint { get; private set; }

    public TargetPickerForm()
    {
        Bounds = new Rectangle(SystemInformation.VirtualScreen.Left - 32000, SystemInformation.VirtualScreen.Top - 32000, 1, 1);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        Opacity = 0;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.SetCrosshairCursor();
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(null), 0);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0);
        if (_mouseHook == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Logger.Info($"Target picker mouse hook failed with Win32 error {error}.");
            MessageBox.Show("TextCrate could not start target selection.", "TextCrate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Cancel();
            return;
        }

        if (_keyboardHook == IntPtr.Zero)
        {
            Logger.Info($"Target picker keyboard hook failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        Logger.Info("Target picker started.");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Unhook();
        Native.RestoreSystemCursors();
        base.OnFormClosed(e);
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == WmLButtonUp)
        {
            var hook = Marshal.PtrToStructure<MouseHookStruct>(lParam);
            BeginInvoke(() => Complete(new Point(hook.Point.X, hook.Point.Y)));
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == WmKeyDown)
        {
            var keyCode = Marshal.ReadInt32(lParam);
            if (keyCode == VkEscape)
            {
                BeginInvoke(Cancel);
                return 1;
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private void Complete(Point point)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        TargetPoint = point;
        DialogResult = DialogResult.OK;
        Logger.Info($"Target picker selected point {point.X},{point.Y}.");
        Close();
    }

    private void Cancel()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        DialogResult = DialogResult.Cancel;
        Logger.Info("Target picker cancelled.");
        Close();
    }

    private void Unhook()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    private delegate IntPtr NativeHookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MouseHookStruct
    {
        public readonly NativePoint Point;
        public readonly uint MouseData;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookType, NativeHookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
