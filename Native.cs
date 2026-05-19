using System.Runtime.InteropServices;
using System.Text;

namespace TextCrate;

internal static class Native
{
    private const int InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const uint KeyEventFScanCode = 0x0008;
    private const uint MapVkToVsc = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public KeyboardInput Keyboard;
        public int Padding1;
        public int Padding2;
    }

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanEx(char ch, IntPtr keyboardLayout);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, IntPtr processId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern bool SetSystemCursor(IntPtr cursor, uint id);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr instance, int cursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyIcon(IntPtr cursor);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(uint action, uint param, string? value, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    private const uint SpiSetCursors = 0x0057;
    private const uint NormalCursor = 32512;
    private const uint IBeamCursor = 32513;
    private const uint HandCursor = 32649;
    private const int CrossCursor = 32515;

    public static void SetProcessDpiAware()
    {
        try
        {
            SetProcessDPIAware();
        }
        catch
        {
            // Older Windows versions can ignore explicit DPI setup.
        }
    }

    public static string GetWindowTitle(IntPtr windowHandle)
    {
        var length = GetWindowTextLength(windowHandle);
        var builder = new StringBuilder(length + 1);
        GetWindowText(windowHandle, builder, builder.Capacity);
        return builder.ToString();
    }

    public static void SetCrosshairCursor()
    {
        try
        {
            foreach (var cursorId in new[] { NormalCursor, IBeamCursor, HandCursor })
            {
                var cursor = CopyIcon(LoadCursor(IntPtr.Zero, CrossCursor));
                if (cursor != IntPtr.Zero)
                {
                    SetSystemCursor(cursor, cursorId);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Could not set crosshair cursor.", ex);
        }
    }

    public static void RestoreSystemCursors()
    {
        try
        {
            SystemParametersInfo(SpiSetCursors, 0, null, 0);
        }
        catch (Exception ex)
        {
            Logger.Error("Could not restore system cursors.", ex);
        }
    }

    public static bool IsModifierKeyPressed()
    {
        return IsKeyPressed(Keys.ShiftKey)
            || IsKeyPressed(Keys.ControlKey)
            || IsKeyPressed(Keys.Menu)
            || IsKeyPressed(Keys.LWin)
            || IsKeyPressed(Keys.RWin);
    }

    private static bool IsKeyPressed(Keys key)
    {
        return (GetKeyState((int)key) & 0x8000) != 0;
    }

    public static void SendText(string text, int delayMs, TypingMethod method, CancellationToken cancellationToken)
    {
        if (method == TypingMethod.ClipboardPaste)
        {
            Clipboard.SetText(text);
            SendKey(Keys.ControlKey, false);
            SendKey(Keys.V, false);
            SendKey(Keys.V, true);
            SendKey(Keys.ControlKey, true);
            return;
        }

        foreach (var ch in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (method == TypingMethod.SendKeys)
            {
                SendKeys.SendWait(ToSendKeysToken(ch));
            }
            else
            {
                SendCharacter(ch);
            }

            if (delayMs > 0)
            {
                Thread.Sleep(delayMs);
            }
        }
    }

    private static string ToSendKeysToken(char character)
    {
        return character switch
        {
            '\r' => string.Empty,
            '\n' => "{ENTER}",
            '\t' => "{TAB}",
            '{' or '}' or '[' or ']' or '+' or '^' or '%' or '~' or '(' or ')' => "{" + character + "}",
            _ => character.ToString()
        };
    }

    private static void SendCharacter(char character)
    {
        switch (character)
        {
            case '\r':
                return;
            case '\n':
                SendKey(Keys.Enter, false);
                SendKey(Keys.Enter, true);
                return;
            case '\t':
                SendKey(Keys.Tab, false);
                SendKey(Keys.Tab, true);
                return;
        }

        var layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
        var result = VkKeyScanEx(character, layout);
        if ((result & 0xff) == 0xff)
        {
            SendUnicodeCharacter(character);
            return;
        }

        var virtualKey = (byte)(result & 0xff);
        var shiftState = (byte)((result >> 8) & 0xff);

        var modifiers = new List<byte>();
        if ((shiftState & 1) != 0) modifiers.Add(0x10);
        if ((shiftState & 2) != 0) modifiers.Add(0x11);
        if ((shiftState & 4) != 0) modifiers.Add(0x12);

        foreach (var modifier in modifiers)
        {
            SendKey((Keys)modifier, false);
        }

        SendKey((Keys)virtualKey, false);
        SendKey((Keys)virtualKey, true);

        for (var i = modifiers.Count - 1; i >= 0; i--)
        {
            SendKey((Keys)modifiers[i], true);
        }
    }

    private static void SendKey(Keys key, bool keyUp)
    {
        var virtualKey = (byte)key;
        var input = new Input
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = (ushort)MapVirtualKey(virtualKey, MapVkToVsc),
                Flags = KeyEventFScanCode | (keyUp ? KeyEventFKeyUp : 0),
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void SendUnicodeCharacter(char character)
    {
        var down = new Input
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                VirtualKey = 0,
                ScanCode = character,
                Flags = KeyEventFUnicode,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        };

        var up = down;
        up.Keyboard.Flags = KeyEventFUnicode | KeyEventFKeyUp;
        SendInput(2, [down, up], Marshal.SizeOf<Input>());
    }
}
