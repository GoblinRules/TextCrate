using System.Runtime.InteropServices;

namespace TextCrate;

internal sealed class HotKeyManager : NativeWindow, IDisposable
{
    private const int WmHotKey = 0x0312;
    private const int NoRepeat = 0x4000;
    private int? _registeredId;
    private int _nextId;

    public event EventHandler? HotKeyPressed;

    public HotKeyManager()
    {
        CreateHandle(new CreateParams());
    }

    public bool Apply(AppSettings settings, out string? error)
    {
        Unregister();
        error = null;

        if (!settings.HotKeyEnabled || string.IsNullOrWhiteSpace(settings.HotKey))
        {
            return true;
        }

        if (!Enum.TryParse(settings.HotKey, out Keys key) || IsModifierOnly(key))
        {
            error = "Choose a normal key for the hotkey.";
            return false;
        }

        var id = ++_nextId;
        var modifiers = (uint)settings.HotKeyModifiers | NoRepeat;
        if (!RegisterHotKey(Handle, id, modifiers, (uint)key))
        {
            error = $"Could not register {Format(settings)}. It may already be used by Windows or another app.";
            return false;
        }

        _registeredId = id;
        return true;
    }

    public void Unregister()
    {
        if (_registeredId is not { } id)
        {
            return;
        }

        UnregisterHotKey(Handle, id);
        _registeredId = null;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey)
        {
            HotKeyPressed?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref m);
    }

    public static string Format(AppSettings settings)
    {
        var parts = new List<string>();
        if (settings.HotKeyModifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (settings.HotKeyModifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (settings.HotKeyModifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        if (settings.HotKeyModifiers.HasFlag(HotKeyModifiers.Windows)) parts.Add("Win");
        if (!string.IsNullOrWhiteSpace(settings.HotKey)) parts.Add(settings.HotKey);
        return string.Join("+", parts);
    }

    private static bool IsModifierOnly(Keys key)
    {
        return key is Keys.Control or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
            or Keys.Alt or Keys.Menu or Keys.LMenu or Keys.RMenu
            or Keys.Shift or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
            or Keys.LWin or Keys.RWin;
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
