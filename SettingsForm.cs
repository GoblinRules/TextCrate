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
    private readonly HotKeyControls _pasteHotKey = new("Enable paste hotkey");
    private readonly HotKeyControls _readHotKey = new("Enable read screen hotkey");
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
        ClientSize = new Size(660, 790);
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
        LoadSettings();
        ApplyTheme();
    }

    private void BuildLayout()
    {
        Controls.Add(new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI Semibold", 16F),
            AutoSize = true,
            Location = new Point(24, 22)
        });

        Controls.Add(new Label
        {
            Text = "Configure typing, OCR, startup behavior, notifications, and shortcuts.",
            AutoSize = false,
            Location = new Point(24, 56),
            Size = new Size(590, 28)
        });

        var content = new Panel
        {
            Location = new Point(24, 96),
            Size = new Size(612, 610),
            AutoScroll = true,
            BackColor = Color.Transparent
        };

        var y = 0;
        y = AddSection(content, y, "General", 2, table =>
        {
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

            _notifications.Text = "Show completion notifications";
            StyleCheckBox(_notifications);
            table.Controls.Add(_notifications, 0, 1);
            table.SetColumnSpan(_notifications, 2);
        });

        y = AddSection(content, y, "Paste", 5, table =>
        {
            _typingMethod.SetOptions(
                new SelectOption("SendInput scan codes", TypingMethod.SendInput),
                new SelectOption("SendKeys compatibility", TypingMethod.SendKeys),
                new SelectOption("Clipboard paste (Ctrl+V)", TypingMethod.ClipboardPaste));
            AddRow(table, 0, "Typing method", _typingMethod);
            AddRow(table, 1, "Delay between keys (ms)", _keyDelay);
            AddRow(table, 2, "Start delay (ms)", _startDelay);

            _confirmLargePaste.Text = "Confirm large paste operations";
            StyleCheckBox(_confirmLargePaste);
            table.Controls.Add(_confirmLargePaste, 0, 3);
            table.SetColumnSpan(_confirmLargePaste, 2);

            AddRow(table, 4, "Confirm over characters", _confirmOver);
        });

        y = AddSection(content, y, "Screen Reading", 2, table =>
        {
            _ocrCleanup.SetOptions(
                new SelectOption("Plain text", OcrCleanupMode.PlainText),
                new SelectOption("Code and .env text", OcrCleanupMode.CodeAndEnvironmentText));
            AddRow(table, 0, "OCR cleanup", _ocrCleanup);

            _enhancedOcr.Text = "Enhanced OCR";
            StyleCheckBox(_enhancedOcr);
            table.Controls.Add(_enhancedOcr, 0, 1);
            table.SetColumnSpan(_enhancedOcr, 2);
        });

        y = AddSection(content, y, "Startup", 2, table =>
        {
            _startWithWindows.Text = "Start with Windows";
            _startAsAdmin.Text = "Start as administrator when launching";
            foreach (var checkBox in new[] { _startWithWindows, _startAsAdmin })
            {
                StyleCheckBox(checkBox);
                table.Controls.Add(checkBox, 0, table.Controls.Count);
                table.SetColumnSpan(checkBox, 2);
            }
        });

        AddSection(content, y, "Hotkeys", 8, table =>
        {
            AddHotKeyRows(table, 0, "Paste", _pasteHotKey);

            _hotKeyMode.SetOptions(
                new SelectOption("Choose target window", HotKeyMode.TargetMode),
                new SelectOption("Type into active window", HotKeyMode.JustStartTyping));
            AddRow(table, 3, "Paste action", _hotKeyMode);

            AddHotKeyRows(table, 4, "Read screen", _readHotKey);
        });

        var buttons = new FlowLayoutPanel
        {
            Location = new Point(24, 724),
            Size = new Size(612, 42),
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
        Controls.Add(content);
        Controls.Add(buttons);

        _toolTip.SetToolTip(_typingMethod, "SendInput is best for VM consoles. SendKeys is a fallback. Clipboard paste uses Ctrl+V when the target supports it.");
        _toolTip.SetToolTip(_theme, "Controls the settings window and tray menu appearance.");
        _toolTip.SetToolTip(_keyDelay, "Use 0 or 1 ms for fast batched typing. Use 2 ms or higher if a remote console drops characters.");
        _toolTip.SetToolTip(_startDelay, "Waits after selecting the target before typing begins.");
        _toolTip.SetToolTip(_ocrCleanup, "Code mode repairs common OCR spacing in environment variables such as DATABASE_URL.");
        _toolTip.SetToolTip(_enhancedOcr, "Runs extra OCR passes for small UI text, table columns, colored status pills, times, and ports.");
        _toolTip.SetToolTip(_startAsAdmin, "Relaunches with UAC elevation when TextCrate starts. Useful for typing into administrator windows.");
        _toolTip.SetToolTip(_confirmOver, "TextCrate asks before typing clipboard text longer than this limit.");
        _toolTip.SetToolTip(_pasteHotKey.Key, "Press a key here. Backspace clears the hotkey.");
        _toolTip.SetToolTip(_readHotKey.Key, "Press a key here. Backspace clears the hotkey.");
        _toolTip.SetToolTip(_hotKeyMode, "Choose whether the paste hotkey asks for a target first or types into the active window.");
    }

    private int AddSection(Panel parent, int y, string title, int rowCount, Action<TableLayoutPanel> build)
    {
        var label = new Label
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 11F),
            AutoSize = true,
            Location = new Point(0, y)
        };
        parent.Controls.Add(label);

        var table = new TableLayoutPanel
        {
            Location = new Point(0, y + 30),
            Size = new Size(588, rowCount * 32),
            ColumnCount = 2,
            RowCount = rowCount,
            BackColor = Color.Transparent
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < rowCount; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        }

        build(table);
        parent.Controls.Add(table);
        return table.Bottom + 22;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private void AddHotKeyRows(TableLayoutPanel table, int startRow, string label, HotKeyControls controls)
    {
        StyleCheckBox(controls.Enabled);
        table.Controls.Add(controls.Enabled, 0, startRow);
        table.SetColumnSpan(controls.Enabled, 2);

        controls.Key.KeyDown += HotKeyOnKeyDown;
        AddRow(table, startRow + 1, $"{label} key", controls.Key);

        var modifiers = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        foreach (var checkBox in controls.ModifierBoxes)
        {
            StyleCheckBox(checkBox);
            checkBox.Margin = new Padding(0, 4, 14, 0);
            modifiers.Controls.Add(checkBox);
        }
        AddRow(table, startRow + 2, $"{label} modifiers", modifiers);
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
        LoadHotKey(_pasteHotKey, _settings.HotKeyEnabled, _settings.HotKey, _settings.HotKeyModifiers);
        LoadHotKey(_readHotKey, _settings.ReadHotKeyEnabled, _settings.ReadHotKey, _settings.ReadHotKeyModifiers);
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
        (_settings.HotKeyEnabled, _settings.HotKey, _settings.HotKeyModifiers) = ReadHotKey(_pasteHotKey);
        (_settings.ReadHotKeyEnabled, _settings.ReadHotKey, _settings.ReadHotKeyModifiers) = ReadHotKey(_readHotKey);
        _settings.HotKeyMode = (HotKeyMode)_hotKeyMode.SelectedValue!;
        _settings.Save();
    }

    private static void LoadHotKey(HotKeyControls controls, bool enabled, string key, HotKeyModifiers modifiers)
    {
        controls.Enabled.Checked = enabled;
        controls.Key.Text = key;
        controls.Control.Checked = modifiers.HasFlag(HotKeyModifiers.Control);
        controls.Alt.Checked = modifiers.HasFlag(HotKeyModifiers.Alt);
        controls.Shift.Checked = modifiers.HasFlag(HotKeyModifiers.Shift);
        controls.Windows.Checked = modifiers.HasFlag(HotKeyModifiers.Windows);
    }

    private static (bool Enabled, string Key, HotKeyModifiers Modifiers) ReadHotKey(HotKeyControls controls)
    {
        var modifiers = HotKeyModifiers.None
            | (controls.Control.Checked ? HotKeyModifiers.Control : HotKeyModifiers.None)
            | (controls.Alt.Checked ? HotKeyModifiers.Alt : HotKeyModifiers.None)
            | (controls.Shift.Checked ? HotKeyModifiers.Shift : HotKeyModifiers.None)
            | (controls.Windows.Checked ? HotKeyModifiers.Windows : HotKeyModifiers.None);
        return (controls.Enabled.Checked, controls.Key.Text.Trim(), modifiers);
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

        foreach (var label in AllControls().OfType<Label>())
        {
            label.ForeColor = label.Font.Size >= 11 ? palette.Text : palette.Text;
        }

        foreach (var checkBox in AllControls().OfType<CheckBox>())
        {
            checkBox.ForeColor = palette.Text;
        }

        foreach (var button in AllControls().OfType<Button>())
        {
            var primary = button.Text == "Save";
            button.BackColor = primary ? palette.Accent : palette.Surface;
            button.ForeColor = primary ? palette.AccentText : palette.Text;
            button.FlatAppearance.BorderColor = primary ? palette.Accent : palette.Border;
        }

        foreach (var selector in AllControls().OfType<ThemedSelect>())
        {
            selector.SetTheme(palette);
        }
    }

    private IEnumerable<Control> AllControls()
    {
        foreach (Control control in Controls)
        {
            foreach (var child in Descendants(control))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        yield return control;
        foreach (Control child in control.Controls)
        {
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
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
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.KeyCode is Keys.Back or Keys.Delete or Keys.Escape)
        {
            textBox.Text = string.Empty;
        }
        else if (e.KeyCode is not (Keys.Control or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
                 or Keys.Alt or Keys.Menu or Keys.LMenu or Keys.RMenu
                 or Keys.Shift or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
                 or Keys.LWin or Keys.RWin))
        {
            textBox.Text = e.KeyCode.ToString();
        }

        e.SuppressKeyPress = true;
    }

    private sealed class HotKeyControls
    {
        public CheckBox Enabled { get; }
        public TextBox Key { get; } = new();
        public CheckBox Control { get; } = new() { Text = "Ctrl" };
        public CheckBox Alt { get; } = new() { Text = "Alt" };
        public CheckBox Shift { get; } = new() { Text = "Shift" };
        public CheckBox Windows { get; } = new() { Text = "Win" };

        public HotKeyControls(string enabledText)
        {
            Enabled = new CheckBox { Text = enabledText };
        }

        public IEnumerable<CheckBox> ModifierBoxes => [Control, Alt, Shift, Windows];
    }
}
