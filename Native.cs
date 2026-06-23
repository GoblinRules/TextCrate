using System.Runtime.InteropServices;
using System.Text;

namespace TextCrate;

internal static class Native
{
    private const int InputKeyboard = 1;
    private const uint KeyEventFExtendedKey = 0x0001;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const uint MapVkToVsc = 0;
    private const byte VkLShift = 0xA0;
    private const byte VkLControl = 0xA2;
    private const byte VkRMenu = 0xA5;

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
    private static extern uint GetKeyboardLayoutList(int count, [Out] IntPtr[]? layouts);

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
            SendKey(Keys.LControlKey, false);
            SendKey(Keys.V, false);
            SendKey(Keys.V, true);
            SendKey(Keys.LControlKey, true);
            return;
        }

        text = NormalizeLineEndings(text);
        var layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
        foreach (var ch in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (method == TypingMethod.SendKeys)
            {
                SendKeys.SendWait(ToSendKeysToken(ch));
            }
            else
            {
                SendCharacter(ch, layout);
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

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static void SendInputBatch(List<Input> inputs)
    {
        const int chunkSize = 512;
        var offset = 0;
        while (offset < inputs.Count)
        {
            var count = Math.Min(chunkSize, inputs.Count - offset);
            var chunk = new Input[count];
            inputs.CopyTo(offset, chunk, 0, count);
            SendInput((uint)count, chunk, Marshal.SizeOf<Input>());
            offset += count;
        }
    }

    private static void SendCharacter(char character, IntPtr layout)
    {
        var inputs = new List<Input>(8);
        AddCharacterInputs(character, layout, inputs);
        SendInputBatch(inputs);
    }

    private static void AddCharacterInputs(char character, IntPtr layout, List<Input> inputs)
    {
        switch (character)
        {
            case '\r':
                return;
            case '\n':
                AddKeyInput(Keys.Enter, false, inputs);
                AddKeyInput(Keys.Enter, true, inputs);
                return;
            case '\t':
                AddKeyInput(Keys.Tab, false, inputs);
                AddKeyInput(Keys.Tab, true, inputs);
                return;
        }

        var result = VkKeyScanEx(character, layout);
        if (IsMissingKeyScan(result) && !TryFindKeyScanInInstalledLayouts(character, layout, out result))
        {
            AddUnicodeCharacterInputs(character, inputs);
            return;
        }

        var virtualKey = (byte)(result & 0xff);
        var shiftState = (byte)((result >> 8) & 0xff);

        var needsShift = (shiftState & 1) != 0;
        var needsControl = (shiftState & 2) != 0;
        var needsAlt = (shiftState & 4) != 0;
        var isAltGr = needsControl && needsAlt;

        if (needsShift)
        {
            AddKeyInput(VkLShift, false, inputs);
        }

        if (isAltGr)
        {
            AddKeyInput(VkRMenu, false, inputs, extended: true);
        }
        else
        {
            if (needsControl)
            {
                AddKeyInput(VkLControl, false, inputs);
            }

            if (needsAlt)
            {
                AddKeyInput(VkRMenu, false, inputs, extended: true);
            }
        }

        AddKeyInput(virtualKey, false, inputs);
        AddKeyInput(virtualKey, true, inputs);

        if (isAltGr)
        {
            AddKeyInput(VkRMenu, true, inputs, extended: true);
        }
        else
        {
            if (needsAlt)
            {
                AddKeyInput(VkRMenu, true, inputs, extended: true);
            }

            if (needsControl)
            {
                AddKeyInput(VkLControl, true, inputs);
            }
        }

        if (needsShift)
        {
            AddKeyInput(VkLShift, true, inputs);
        }
    }

    private static bool TryFindKeyScanInInstalledLayouts(char character, IntPtr activeLayout, out short result)
    {
        result = -1;
        var count = (int)GetKeyboardLayoutList(0, null);
        if (count <= 0)
        {
            return false;
        }

        var layouts = new IntPtr[count];
        GetKeyboardLayoutList(count, layouts);
        foreach (var layout in layouts)
        {
            if (layout == activeLayout)
            {
                continue;
            }

            var layoutResult = VkKeyScanEx(character, layout);
            if (!IsMissingKeyScan(layoutResult))
            {
                result = layoutResult;
                return true;
            }
        }

        return false;
    }

    private static bool IsMissingKeyScan(short result)
    {
        return (result & 0xff) == 0xff && ((result >> 8) & 0xff) == 0xff;
    }

    private static void SendKey(Keys key, bool keyUp)
    {
        var inputs = new List<Input>(1);
        AddKeyInput(key, keyUp, inputs);
        SendInputBatch(inputs);
    }

    private static void AddKeyInput(Keys key, bool keyUp, List<Input> inputs)
    {
        AddKeyInput((byte)key, keyUp, inputs);
    }

    private static void AddKeyInput(byte virtualKey, bool keyUp, List<Input> inputs, bool extended = false)
    {
        inputs.Add(new Input
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = (ushort)MapVirtualKey(virtualKey, MapVkToVsc),
                Flags = (keyUp ? KeyEventFKeyUp : 0) | (extended ? KeyEventFExtendedKey : 0),
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        });
    }

    private static void SendUnicodeCharacter(char character)
    {
        var inputs = new List<Input>(2);
        AddUnicodeCharacterInputs(character, inputs);
        SendInputBatch(inputs);
    }

    private static void AddUnicodeCharacterInputs(char character, List<Input> inputs)
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
        inputs.Add(down);
        inputs.Add(up);
    }
}
