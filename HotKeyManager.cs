using System.Runtime.InteropServices;

namespace TextCrate;

internal sealed class HotKeyManager : NativeWindow, IDisposable
{
    private const int WmHotKey = 0x0312;
    private const int NoRepeat = 0x4000;
    private readonly Dictionary<int, HotKeyAction> _registeredIds = [];
    private int _nextId;

    public event EventHandler<HotKeyPressedEventArgs>? HotKeyPressed;

    public HotKeyManager()
    {
        CreateHandle(new CreateParams());
    }

    public bool Apply(AppSettings settings, out string? error)
    {
        Unregister();
        error = null;

        if (!TryRegister(
                settings.HotKeyEnabled,
                settings.HotKey,
                settings.HotKeyModifiers,
                HotKeyAction.Paste,
                out error))
        {
            return false;
        }

        if (!TryRegister(
                settings.ReadHotKeyEnabled,
                settings.ReadHotKey,
                settings.ReadHotKeyModifiers,
                HotKeyAction.ReadScreenArea,
                out error))
        {
            Unregister();
            return false;
        }

        return true;
    }

    private bool TryRegister(bool enabled, string keyName, HotKeyModifiers hotKeyModifiers, HotKeyAction action, out string? error)
    {
        error = null;
        if (!enabled || string.IsNullOrWhiteSpace(keyName))
        {
            return true;
        }

        if (!Enum.TryParse(keyName, out Keys key) || IsModifierOnly(key))
        {
            error = $"Choose a normal key for the {FormatAction(action)} hotkey.";
            return false;
        }

        var id = ++_nextId;
        var modifiers = (uint)hotKeyModifiers | NoRepeat;
        if (!RegisterHotKey(Handle, id, modifiers, (uint)key))
        {
            error = $"Could not register {Format(keyName, hotKeyModifiers)} for {FormatAction(action)}. It may already be used by Windows or another app.";
            return false;
        }

        _registeredIds[id] = action;
        return true;
    }

    public void Unregister()
    {
        foreach (var id in _registeredIds.Keys)
        {
            UnregisterHotKey(Handle, id);
        }

        _registeredIds.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey)
        {
            var id = m.WParam.ToInt32();
            if (_registeredIds.TryGetValue(id, out var action))
            {
                HotKeyPressed?.Invoke(this, new HotKeyPressedEventArgs(action));
            }
        }

        base.WndProc(ref m);
    }

    public static string Format(AppSettings settings)
    {
        return Format(settings.HotKey, settings.HotKeyModifiers);
    }

    public static string Format(string keyName, HotKeyModifiers modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotKeyModifiers.Windows)) parts.Add("Win");
        if (!string.IsNullOrWhiteSpace(keyName)) parts.Add(keyName);
        return string.Join("+", parts);
    }

    private static string FormatAction(HotKeyAction action)
    {
        return action == HotKeyAction.ReadScreenArea ? "read screen area" : "paste";
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

internal sealed class HotKeyPressedEventArgs(HotKeyAction action) : EventArgs
{
    public HotKeyAction Action { get; } = action;
}
