namespace TextCrate;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly ThemedSelect _typingMethod = new();
    private readonly ThemedSelect _theme = new();
    private readonly ThemedSelect _ocrCleanup = new();
    private readonly ThemedSelect _hotKeyMode = new();
    private readonly TextBox _keyDelay = new();
    private readonly TextBox _startDelay = new();
    private readonly CheckBox _notifications = new();
    private readonly CheckBox _startWithWindows = new();
    private readonly CheckBox _startAsAdmin = new();
    private readonly CheckBox _confirmLargePaste = new();
    private readonly TextBox _confirmOver = new();
    private readonly CheckBox _enhancedOcr = new();
    private readonly CheckBox _hotKeyEnabled = new();
    private readonly TextBox _hotKey = new();
    private readonly CheckBox _hotKeyAlt = new();
    private readonly CheckBox _hotKeyControl = new();
    private readonly CheckBox _hotKeyShift = new();
    private readonly CheckBox _hotKeyWindows = new();
    private readonly ToolTip _toolTip = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "TextCrate Settings";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "icon.ico"));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(600, 700);
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
        LoadSettings();
        ApplyTheme();
    }

    private void BuildLayout()
    {
        var header = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = Color.FromArgb(15, 23, 42),
            AutoSize = true,
            Location = new Point(24, 22)
        };

        var subtitle = new Label
        {
            Text = "Configure how TextCrate types into remote windows and reads selected screen areas.",
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = false,
            Location = new Point(24, 56),
            Size = new Size(535, 36)
        };

        var table = new TableLayoutPanel
        {
            Location = new Point(24, 108),
            Size = new Size(552, 500),
            ColumnCount = 2,
            RowCount = 15,
            BackColor = Color.Transparent
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 15; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        }

        _theme.SetOptions(
            new SelectOption("Use system theme", AppTheme.System),
            new SelectOption("Light", AppTheme.Light),
            new SelectOption("Dark", AppTheme.Dark));
        _theme.SelectedValueChanged += (_, _) =>
        {
            if (_theme.SelectedValue is AppTheme theme)
            {
                _settings.Theme = theme;
                ApplyTheme();
            }
        };
        AddRow(table, 0, "Theme", _theme);

        _typingMethod.SetOptions(
            new SelectOption("SendInput scan codes", TypingMethod.SendInput),
            new SelectOption("SendKeys compatibility", TypingMethod.SendKeys),
            new SelectOption("Clipboard paste (Ctrl+V)", TypingMethod.ClipboardPaste));
        AddRow(table, 1, "Typing method", _typingMethod);

        AddRow(table, 2, "Delay between keys (ms)", _keyDelay);

        AddRow(table, 3, "Start delay (ms)", _startDelay);

        _ocrCleanup.SetOptions(
            new SelectOption("Plain text", OcrCleanupMode.PlainText),
            new SelectOption("Code and .env text", OcrCleanupMode.CodeAndEnvironmentText));
        AddRow(table, 4, "OCR cleanup", _ocrCleanup);

        _enhancedOcr.Text = "Enhanced OCR";
        StyleCheckBox(_enhancedOcr);
        table.Controls.Add(_enhancedOcr, 0, 5);
        table.SetColumnSpan(_enhancedOcr, 2);

        _notifications.Text = "Show completion notifications";
        StyleCheckBox(_notifications);
        table.Controls.Add(_notifications, 0, 6);
        table.SetColumnSpan(_notifications, 2);

        _startWithWindows.Text = "Start with Windows";
        StyleCheckBox(_startWithWindows);
        table.Controls.Add(_startWithWindows, 0, 7);
        table.SetColumnSpan(_startWithWindows, 2);

        _startAsAdmin.Text = "Start as administrator when launching";
        StyleCheckBox(_startAsAdmin);
        table.Controls.Add(_startAsAdmin, 0, 8);
        table.SetColumnSpan(_startAsAdmin, 2);

        _confirmLargePaste.Text = "Confirm large paste operations";
        StyleCheckBox(_confirmLargePaste);
        table.Controls.Add(_confirmLargePaste, 0, 9);
        table.SetColumnSpan(_confirmLargePaste, 2);

        AddRow(table, 10, "Confirm over characters", _confirmOver);

        _hotKeyEnabled.Text = "Enable paste hotkey";
        StyleCheckBox(_hotKeyEnabled);
        table.Controls.Add(_hotKeyEnabled, 0, 11);
        table.SetColumnSpan(_hotKeyEnabled, 2);

        _hotKey.KeyDown += HotKeyOnKeyDown;
        AddRow(table, 12, "Hotkey key", _hotKey);

        var modifiers = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _hotKeyControl.Text = "Ctrl";
        _hotKeyAlt.Text = "Alt";
        _hotKeyShift.Text = "Shift";
        _hotKeyWindows.Text = "Win";
        foreach (var checkBox in new[] { _hotKeyControl, _hotKeyAlt, _hotKeyShift, _hotKeyWindows })
        {
            StyleCheckBox(checkBox);
            checkBox.Margin = new Padding(0, 4, 14, 0);
            modifiers.Controls.Add(checkBox);
        }
        AddRow(table, 13, "Hotkey modifiers", modifiers);

        _hotKeyMode.SetOptions(
            new SelectOption("Choose target window", HotKeyMode.TargetMode),
            new SelectOption("Type into active window", HotKeyMode.JustStartTyping));
        AddRow(table, 14, "Hotkey action", _hotKeyMode);

        var buttons = new FlowLayoutPanel
        {
            Location = new Point(24, 632),
            Size = new Size(552, 42),
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent
        };
        var ok = CreateButton("Save", true);
        var cancel = CreateButton("Cancel", false);
        ok.Click += (_, _) => SaveSettings();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(header);
        Controls.Add(subtitle);
        Controls.Add(table);
        Controls.Add(buttons);

        _toolTip.SetToolTip(_typingMethod, "SendInput is best for VM consoles. SendKeys is a fallback. Clipboard paste uses Ctrl+V when the target supports it.");
        _toolTip.SetToolTip(_theme, "Controls the settings window and tray menu appearance.");
        _toolTip.SetToolTip(_keyDelay, "Adds a small pause between typed characters for slow remote consoles.");
        _toolTip.SetToolTip(_startDelay, "Waits after selecting the target before typing begins.");
        _toolTip.SetToolTip(_ocrCleanup, "Code mode repairs common OCR spacing in environment variables such as DATABASE_URL.");
        _toolTip.SetToolTip(_enhancedOcr, "Runs extra internal OCR passes for small UI text, table columns, colored status pills, times, and ports.");
        _toolTip.SetToolTip(_startAsAdmin, "Relaunches with UAC elevation when TextCrate starts. Useful for typing into administrator windows.");
        _toolTip.SetToolTip(_confirmOver, "TextCrate asks before typing clipboard text longer than this limit.");
        _toolTip.SetToolTip(_hotKey, "Press a key here. Backspace clears the hotkey.");
        _toolTip.SetToolTip(_hotKeyMode, "Choose whether the hotkey asks for a target first or types into the active window.");
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.FromArgb(30, 41, 59)
        }, 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private void LoadSettings()
    {
        _theme.SelectedValue = _settings.Theme;
        _typingMethod.SelectedValue = _settings.TypingMethod;
        _keyDelay.Text = Math.Clamp(_settings.KeyDelayMs, 0, 250).ToString();
        _startDelay.Text = Math.Clamp(_settings.StartDelayMs, 0, 5000).ToString();
        _notifications.Checked = _settings.ShowNotifications;
        _startWithWindows.Checked = StartupRegistration.IsEnabled();
        _startAsAdmin.Checked = _settings.StartAsAdmin;
        _confirmLargePaste.Checked = _settings.ConfirmLargePaste;
        _confirmOver.Text = Math.Clamp(_settings.ConfirmLargePasteOver, 1, 1000000).ToString();
        _ocrCleanup.SelectedValue = _settings.OcrCleanupMode;
        _enhancedOcr.Checked = _settings.EnhancedOcr;
        _hotKeyEnabled.Checked = _settings.HotKeyEnabled;
        _hotKey.Text = _settings.HotKey;
        _hotKeyControl.Checked = _settings.HotKeyModifiers.HasFlag(HotKeyModifiers.Control);
        _hotKeyAlt.Checked = _settings.HotKeyModifiers.HasFlag(HotKeyModifiers.Alt);
        _hotKeyShift.Checked = _settings.HotKeyModifiers.HasFlag(HotKeyModifiers.Shift);
        _hotKeyWindows.Checked = _settings.HotKeyModifiers.HasFlag(HotKeyModifiers.Windows);
        _hotKeyMode.SelectedValue = _settings.HotKeyMode;
    }

    private void SaveSettings()
    {
        _settings.TypingMethod = (TypingMethod)_typingMethod.SelectedValue!;
        _settings.Theme = (AppTheme)_theme.SelectedValue!;
        _settings.KeyDelayMs = ReadInt(_keyDelay, 1, 0, 250);
        _settings.StartDelayMs = ReadInt(_startDelay, 150, 0, 5000);
        _settings.ShowNotifications = _notifications.Checked;
        _settings.StartWithWindows = _startWithWindows.Checked;
        _settings.StartAsAdmin = _startAsAdmin.Checked;
        _settings.ConfirmLargePaste = _confirmLargePaste.Checked;
        _settings.ConfirmLargePasteOver = ReadInt(_confirmOver, 500, 1, 1000000);
        _settings.OcrCleanupMode = (OcrCleanupMode)_ocrCleanup.SelectedValue!;
        _settings.EnhancedOcr = _enhancedOcr.Checked;
        _settings.HotKeyEnabled = _hotKeyEnabled.Checked;
        _settings.HotKey = _hotKey.Text.Trim();
        _settings.HotKeyModifiers = HotKeyModifiers.None
            | (_hotKeyControl.Checked ? HotKeyModifiers.Control : HotKeyModifiers.None)
            | (_hotKeyAlt.Checked ? HotKeyModifiers.Alt : HotKeyModifiers.None)
            | (_hotKeyShift.Checked ? HotKeyModifiers.Shift : HotKeyModifiers.None)
            | (_hotKeyWindows.Checked ? HotKeyModifiers.Windows : HotKeyModifiers.None);
        _settings.HotKeyMode = (HotKeyMode)_hotKeyMode.SelectedValue!;
        _settings.Save();
    }

    private static Button CreateButton(string text, bool primary)
    {
        return new Button
        {
            Text = text,
            DialogResult = primary ? DialogResult.OK : DialogResult.Cancel,
            Width = 104,
            Height = 34,
            Margin = new Padding(8, 0, 0, 0),
            BackColor = primary ? Color.FromArgb(14, 116, 144) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(15, 23, 42),
            FlatStyle = FlatStyle.Flat
        };
    }

    private void ApplyTheme()
    {
        var palette = ThemeService.GetPalette(_settings);
        ThemeService.ApplyToControl(this, palette);

        foreach (var label in Controls.OfType<Label>())
        {
            label.ForeColor = label.Font.Size >= 14 ? palette.Text : palette.MutedText;
        }

        foreach (var label in Controls.OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Label>()))
        {
            label.ForeColor = palette.Text;
        }

        foreach (var checkBox in Controls.OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<CheckBox>()))
        {
            checkBox.ForeColor = palette.Text;
        }

        foreach (var button in Controls.OfType<FlowLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Button>()))
        {
            var primary = button.Text == "Save";
            button.BackColor = primary ? palette.Accent : palette.Surface;
            button.ForeColor = primary ? palette.AccentText : palette.Text;
            button.FlatAppearance.BorderColor = primary ? palette.Accent : palette.Border;
        }

        foreach (var selector in Controls.OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<ThemedSelect>()))
        {
            selector.SetTheme(palette);
        }
    }

    private static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.ForeColor = Color.FromArgb(30, 41, 59);
        checkBox.AutoSize = true;
        checkBox.Anchor = AnchorStyles.Left;
    }

    private static int ReadInt(TextBox textBox, int fallback, int minimum, int maximum)
    {
        return int.TryParse(textBox.Text.Trim(), out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }

    private void HotKeyOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Back or Keys.Delete or Keys.Escape)
        {
            _hotKey.Text = string.Empty;
        }
        else if (e.KeyCode is not (Keys.Control or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
                 or Keys.Alt or Keys.Menu or Keys.LMenu or Keys.RMenu
                 or Keys.Shift or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
                 or Keys.LWin or Keys.RWin))
        {
            _hotKey.Text = e.KeyCode.ToString();
        }

        e.SuppressKeyPress = true;
    }
}
