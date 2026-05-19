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
    private readonly Dictionary<string, Button> _tabButtons = [];
    private readonly Dictionary<string, Panel> _pages = [];
    private string _activeTab = "General";

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "TextCrate Settings";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "icon.ico"));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(700, 700);
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
        LoadSettings();
        ApplyTheme();
        ShowPage(_activeTab);
    }

    private void BuildLayout()
    {
        Controls.Add(new Label
        {
            Text = "Settings",
            UseMnemonic = false,
            Font = new Font("Segoe UI Semibold", 16F),
            AutoSize = true,
            Location = new Point(24, 22)
        });

        Controls.Add(new Label
        {
            Text = "Configure typing, OCR, startup behavior, notifications, shortcuts, and help.",
            UseMnemonic = false,
            AutoSize = false,
            Location = new Point(24, 56),
            Size = new Size(620, 24)
        });

        var tabs = new FlowLayoutPanel
        {
            Location = new Point(24, 92),
            Size = new Size(652, 42),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        foreach (var tab in new[] { "General", "Paste", "OCR", "Startup", "Hotkeys", "Help & About" })
        {
            var button = new Button
            {
                Text = tab,
                UseMnemonic = false,
                Width = tab == "Help & About" ? 118 : 86,
                Height = 32,
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat
            };
            button.Click += (_, _) => ShowPage(tab);
            _tabButtons[tab] = button;
            tabs.Controls.Add(button);
        }
        Controls.Add(tabs);

        BuildGeneralPage();
        BuildPastePage();
        BuildOcrPage();
        BuildStartupPage();
        BuildHotkeysPage();
        BuildHelpPage();

        var buttons = new FlowLayoutPanel
        {
            Location = new Point(24, 628),
            Size = new Size(652, 42),
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

    private void BuildGeneralPage()
    {
        var page = CreatePage("General");
        AddSectionTitle(page, "General", 0);
        var table = CreateTable(page, 40, 2);

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
    }

    private void BuildPastePage()
    {
        var page = CreatePage("Paste");
        AddSectionTitle(page, "Paste", 0);
        AddInfo(page, "Left-click the tray icon to choose a target, or use the paste hotkey. TextCrate types the current clipboard into the target window.", 32, 560);

        var table = CreateTable(page, 122, 5);
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

        AddInfo(page, "Tip: 0 or 1 ms uses the fast batched SendInput path. Use 2 ms or more if a slow VM console drops characters.", 314, 590);
    }

    private void BuildOcrPage()
    {
        var page = CreatePage("OCR");
        AddSectionTitle(page, "Screen Reading", 0);
        AddInfo(page, "Read screen area copies text from a rectangle you draw on screen. It is intended for remote VMs and dashboards where clipboard sharing is unavailable.", 32, 570);

        var table = CreateTable(page, 122, 2);
        _ocrCleanup.SetOptions(
            new SelectOption("Plain text", OcrCleanupMode.PlainText),
            new SelectOption("Code and .env text", OcrCleanupMode.CodeAndEnvironmentText),
            new SelectOption("Passwords and tokens", OcrCleanupMode.PasswordsAndTokens));
        AddRow(table, 0, "OCR cleanup", _ocrCleanup);

        _enhancedOcr.Text = "Enhanced OCR";
        StyleCheckBox(_enhancedOcr);
        table.Controls.Add(_enhancedOcr, 0, 1);
        table.SetColumnSpan(_enhancedOcr, 2);

        AddSubheading(page, "How it works", 186);
        AddInfo(page, "TextCrate captures only the selected screen area, runs bundled Tesseract OCR first, then tries Windows OCR and extra image passes when enhanced OCR is enabled. It scores the results and copies the best text to your clipboard.", 214, 570);

        AddSubheading(page, "Practical limits", 308);
        AddInfo(page, "Small fonts, low contrast, anti-aliased browser text, and colored status pills can still be misread. Tight selections usually improve accuracy. Passwords and tokens keeps full QWERTY symbols and removes spaces; code mode is best for .env keys, URLs, ports, and timestamps.", 336, 570);
    }

    private void BuildStartupPage()
    {
        var page = CreatePage("Startup");
        AddSectionTitle(page, "Startup", 0);
        AddInfo(page, "Some administrator windows ignore input from normal apps. Running TextCrate as administrator lets it type into elevated tools when needed. For one-off use, right-click the tray icon and choose Relaunch as administrator instead of enabling admin startup. Startup options are stored per Windows user; administrator launch will still show a UAC prompt because the app is unsigned.", 32, 590);

        var table = CreateTable(page, 154, 2);
        _startWithWindows.Text = "Start with Windows";
        _startAsAdmin.Text = "Start as administrator when launching";
        StyleCheckBox(_startWithWindows);
        StyleCheckBox(_startAsAdmin);
        table.Controls.Add(_startWithWindows, 0, 0);
        table.SetColumnSpan(_startWithWindows, 2);
        table.Controls.Add(_startAsAdmin, 0, 1);
        table.SetColumnSpan(_startAsAdmin, 2);
    }

    private void BuildHotkeysPage()
    {
        var page = CreatePage("Hotkeys");
        AddSectionTitle(page, "Hotkeys", 0);
        AddInfo(page, "Hotkeys are registered with Windows. If another app already owns a shortcut, TextCrate will warn when you save.", 32, 560);

        var table = CreateTable(page, 122, 8);
        AddHotKeyRows(table, 0, "Paste", _pasteHotKey);

        _hotKeyMode.SetOptions(
            new SelectOption("Choose target window", HotKeyMode.TargetMode),
            new SelectOption("Type into active window", HotKeyMode.JustStartTyping));
        AddRow(table, 3, "Paste action", _hotKeyMode);

        AddHotKeyRows(table, 4, "Read screen", _readHotKey);
    }

    private void BuildHelpPage()
    {
        var page = CreatePage("Help & About");
        AddSectionTitle(page, "Help & About", 0);
        AddInfo(page, $"TextCrate {GetVersion()}\nPublisher: Goblin Rules\nGhost Kernel: https://ghostkernel.cc", 32, 590);

        AddSubheading(page, "Quick actions", 112);
        AddInfo(page, "Left-click tray icon: paste clipboard to a target.\nRight-click tray icon: paste, read screen area, relaunch as administrator, settings, cancel typing, and exit.\nEsc: cancel target or region selection.", 140, 570);

        AddSubheading(page, "Troubleshooting", 254);
        AddInfo(page, "If text types into the wrong place, use target mode and click directly inside the destination input area.\nIf characters are missed, increase the delay between keys.\nIf OCR misses text, draw a tighter rectangle and keep enhanced OCR enabled.", 282, 590);

        var openLog = CreateButton("Open Log", false);
        openLog.Location = new Point(0, 386);
        openLog.Click += (_, _) => OpenLogFile();
        page.Controls.Add(openLog);
    }

    private Panel CreatePage(string name)
    {
        var page = new Panel
        {
            Location = new Point(24, 146),
            Size = new Size(652, 462),
            BackColor = Color.Transparent,
            Visible = false
        };
        _pages[name] = page;
        Controls.Add(page);
        return page;
    }

    private static TableLayoutPanel CreateTable(Control parent, int y, int rowCount)
    {
        var table = new TableLayoutPanel
        {
            Location = new Point(0, y),
            Size = new Size(624, rowCount * 34),
            ColumnCount = 2,
            RowCount = rowCount,
            BackColor = Color.Transparent
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < rowCount; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }

        parent.Controls.Add(table);
        return table;
    }

    private static void AddSectionTitle(Control parent, string text, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            UseMnemonic = false,
            Font = new Font("Segoe UI Semibold", 12F),
            AutoSize = true,
            Location = new Point(0, y)
        });
    }

    private static void AddSubheading(Control parent, string text, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            UseMnemonic = false,
            Font = new Font("Segoe UI Semibold", 10F),
            AutoSize = true,
            Location = new Point(0, y)
        });
    }

    private static void AddInfo(Control parent, string text, int y, int width)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            UseMnemonic = false,
            AutoSize = false,
            Location = new Point(0, y),
            Size = new Size(width, 86)
        });
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, row);
        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(0, 3, 0, 3);
        control.Width = 396;
        if (control is ThemedSelect)
        {
            control.Height = 28;
        }
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

    private void ShowPage(string name)
    {
        _activeTab = name;
        foreach (var (pageName, page) in _pages)
        {
            page.Visible = pageName == name;
        }
        ApplyTheme();
    }

    private void OpenLogFile()
    {
        try
        {
            Logger.EnsureLogFile();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Logger.LogPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "TextCrate Log", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string GetVersion()
    {
        return Application.ProductVersion.Split('+')[0];
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
            label.ForeColor = label.Font.Size >= 10 ? palette.Text : palette.MutedText;
        }

        foreach (var checkBox in AllControls().OfType<CheckBox>())
        {
            checkBox.ForeColor = palette.Text;
        }

        foreach (var button in AllControls().OfType<Button>())
        {
            var primary = button.Text == "Save";
            var activeTab = _tabButtons.TryGetValue(_activeTab, out var active) && ReferenceEquals(button, active);
            button.BackColor = primary || activeTab ? palette.Accent : palette.Surface;
            button.ForeColor = primary || activeTab ? palette.AccentText : palette.Text;
            button.FlatAppearance.BorderColor = primary || activeTab ? palette.Accent : palette.Border;
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
