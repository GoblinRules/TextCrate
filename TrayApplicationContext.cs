using System.Media;
using System.Runtime.InteropServices;

namespace TextCrate;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AppSettings _settings;
    private readonly SynchronizationContext _uiContext;
    private readonly Icon _normalIcon;
    private readonly System.Windows.Forms.Timer _activityTimer;
    private readonly HotKeyManager _hotKeyManager;
    private CancellationTokenSource _typingCancellation = new();
    private bool _busy;
    private bool _activityDotVisible;
    private Icon? _activityIcon;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        _settings.StartWithWindows = StartupRegistration.IsEnabled();
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _normalIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "icon.ico"));
        _activityTimer = new System.Windows.Forms.Timer { Interval = 450 };
        _activityTimer.Tick += (_, _) => ToggleActivityIcon();
        _hotKeyManager = new HotKeyManager();
        _hotKeyManager.HotKeyPressed += HotKeyManagerOnHotKeyPressed;
        _notifyIcon = new NotifyIcon
        {
            Icon = _normalIcon,
            Text = "TextCrate",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.MouseUp += NotifyIconOnMouseUp;
        ApplyHotKey(showErrors: false);
    }

    private ContextMenuStrip BuildMenu()
    {
        var palette = ThemeService.GetPalette(_settings);
        var menu = new ContextMenuStrip
        {
            BackColor = palette.Surface,
            ForeColor = palette.Text,
            Renderer = new ThemedMenuRenderer(palette)
        };
        menu.Items.Add("Paste clipboard to selected target", null, async (_, _) => await StartTargetPasteAsync());
        menu.Items.Add("Read screen area to clipboard", null, async (_, _) => await ReadScreenAreaAsync());
        menu.Items.Add(new ToolStripSeparator());
        var elevated = ElevationService.IsAdministrator();
        var elevateItem = menu.Items.Add(elevated ? "Running as administrator" : "Relaunch as administrator", null, (_, _) => RelaunchAsAdministrator());
        elevateItem.Enabled = !elevated;
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Cancel typing", null, (_, _) => CancelTyping());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        return menu;
    }

    private void NotifyIconOnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _ = StartTargetPasteAsync();
        }
    }

    private async Task StartTargetPasteAsync()
    {
        if (_busy)
        {
            SystemSounds.Beep.Play();
            return;
        }

        BeginBusy("TextCrate: preparing paste");

        if (!TryGetClipboardText(out var text))
        {
            EndBusy();
            return;
        }

        text = await PrepareRelayTextAsync(text);
        if (string.IsNullOrEmpty(text))
        {
            EndBusy();
            return;
        }

        if (!ConfirmTyping(text, "the target you choose next"))
        {
            Logger.Info($"Typing declined before target selection with {text.Length} characters.");
            EndBusy();
            return;
        }

        using var targetPicker = new TargetPickerForm();
        if (targetPicker.ShowDialog() != DialogResult.OK || targetPicker.TargetPoint is not { } point)
        {
            EndBusy();
            return;
        }

        Thread.Sleep(120);
        var target = Native.GetForegroundWindow();
        if (target == IntPtr.Zero)
        {
            target = Native.WindowFromPoint(point);
        }

        if (target == IntPtr.Zero)
        {
            SystemSounds.Beep.Play();
            Logger.Info("Target paste cancelled because no target window was found.");
            EndBusy();
            return;
        }

        var targetTitle = Native.GetWindowTitle(target);
        Logger.Info($"Typing {text.Length} characters into target '{targetTitle}'.");
        Native.SetForegroundWindow(target);
        StartTypingThread(text, busyAlreadyStarted: true);
    }

    private async Task StartTypingActiveWindowAsync()
    {
        if (_busy)
        {
            SystemSounds.Beep.Play();
            return;
        }

        if (!TryGetClipboardText(out var text))
        {
            return;
        }

        BeginBusy("TextCrate: preparing paste");
        text = await PrepareRelayTextAsync(text);
        if (string.IsNullOrEmpty(text))
        {
            EndBusy();
            return;
        }

        if (!ConfirmTyping(text, Native.GetWindowTitle(Native.GetForegroundWindow())))
        {
            EndBusy();
            return;
        }

        StartTypingThread(text, busyAlreadyStarted: true);
    }

    private async Task<string> PrepareRelayTextAsync(string text)
    {
        if (!LongTextRelayService.ShouldOffer(text, _settings))
        {
            return text;
        }

        StopActivityIndicator();
        using var prompt = new LongTextRelayPromptForm(_settings, text.Length);
        if (prompt.ShowDialog() != DialogResult.OK)
        {
            StartActivityIndicator();
            return text;
        }

        StartActivityIndicator();
        SetNotifyText("TextCrate: uploading encrypted relay");
        try
        {
            using var uploadCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var relay = await LongTextRelayService.CreateAsync(
                text,
                new LongTextRelayOptions(
                    _settings.LongTextRelayEndpoint,
                    prompt.ExpiryMinutes,
                    prompt.BurnAfterRead,
                    prompt.Password),
                uploadCancellation.Token);
            Logger.Info($"Created Long Text Relay URL for {text.Length} characters.");
            return relay.Url;
        }
        catch (Exception ex)
        {
            Logger.Error("Long Text Relay upload failed.", ex);
            MessageBox.Show("Long Text Relay upload failed. TextCrate will not upload or type anything.", "TextCrate Long Text Relay", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return string.Empty;
        }
    }

    private void StartTypingThread(string text, bool busyAlreadyStarted)
    {
        var thread = new Thread(() => TypeText(text, busyAlreadyStarted));
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    private bool TryGetClipboardText(out string text)
    {
        text = string.Empty;
        try
        {
            text = Clipboard.GetText();
        }
        catch
        {
            SystemSounds.Beep.Play();
            Logger.Info("Could not read text from clipboard.");
            return false;
        }

        if (!string.IsNullOrEmpty(text))
        {
            return true;
        }

        SystemSounds.Beep.Play();
        Logger.Info("Clipboard did not contain text.");
        return false;
    }

    private void TypeText(string text, bool busyAlreadyStarted)
    {
        if (!busyAlreadyStarted)
        {
            BeginBusy("TextCrate: typing");
        }

        SetNotifyText("TextCrate: typing");
        using var cancellation = new CancellationTokenSource();
        _typingCancellation = cancellation;

        try
        {
            Thread.Sleep(_settings.StartDelayMs);
            Native.SendText(text, _settings.KeyDelayMs, _settings.TypingMethod, cancellation.Token);
            Logger.Info($"Typed {text.Length} characters.");
        }
        catch (Exception ex)
        {
            SystemSounds.Beep.Play();
            Logger.Error("Typing failed.", ex);
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task ReadScreenAreaAsync()
    {
        if (_busy)
        {
            SystemSounds.Beep.Play();
            return;
        }

        using var selector = new RegionSelectionForm();
        if (selector.ShowDialog() != DialogResult.OK || selector.SelectedRegion is not { } region)
        {
            return;
        }

        _busy = true;
        StartActivityIndicator();
        _notifyIcon.Text = "TextCrate: reading area";

        try
        {
            var text = await Task.Run(async () => await OcrService.ReadScreenRegionAsync(region, _settings));
            if (string.IsNullOrWhiteSpace(text))
            {
                SystemSounds.Beep.Play();
                Logger.Info("OCR returned no text.");
                return;
            }

            Clipboard.SetText(text.Trim());
            Logger.Info($"OCR copied {text.Trim().Length} characters.");
            if (_settings.ShowNotifications)
            {
                StopActivityIndicator();
                _notifyIcon.ShowBalloonTip(2500, "TextCrate", "Text copied from selected screen area.", ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("OCR failed.", ex);
            MessageBox.Show(ex.Message, "TextCrate OCR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
            StopActivityIndicator();
            _notifyIcon.Text = "TextCrate";
        }
    }

    private void CancelTyping()
    {
        _typingCancellation.Cancel();
    }

    private void ShowSettings()
    {
        var originalTheme = _settings.Theme;
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _notifyIcon.ContextMenuStrip = BuildMenu();
            ApplyHotKey(showErrors: true);
        }
        else if (_settings.Theme != originalTheme)
        {
            _settings.Theme = originalTheme;
            _notifyIcon.ContextMenuStrip = BuildMenu();
        }
    }

    private void ApplyHotKey(bool showErrors)
    {
        if (_hotKeyManager.Apply(_settings, out var error) || !showErrors)
        {
            return;
        }

        MessageBox.Show(error, "TextCrate Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private bool ConfirmTyping(string text, string targetTitle)
    {
        if (!_settings.ConfirmLargePaste || text.Length <= _settings.ConfirmLargePasteOver)
        {
            return true;
        }

        using var form = new ThemedConfirmForm(_settings, text.Length, targetTitle);
        return form.ShowDialog() == DialogResult.Yes;
    }

    private void RelaunchAsAdministrator()
    {
        if (ElevationService.RelaunchAsAdministrator())
        {
            Exit();
        }
        else
        {
            MessageBox.Show("TextCrate could not relaunch as administrator.", "TextCrate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void HotKeyManagerOnHotKeyPressed(object? sender, HotKeyPressedEventArgs e)
    {
        Task.Run(() =>
        {
            while (Native.IsModifierKeyPressed())
            {
                Thread.Sleep(50);
            }

            _uiContext.Post(_ =>
            {
                if (e.Action == HotKeyAction.ReadScreenArea)
                {
                    _ = ReadScreenAreaAsync();
                }
                else if (_settings.HotKeyMode == HotKeyMode.JustStartTyping)
                {
                    _ = StartTypingActiveWindowAsync();
                }
                else
                {
                    _ = StartTargetPasteAsync();
                }
            }, null);
        });
    }

    private void SetNotifyText(string text)
    {
        _uiContext.Post(_ => _notifyIcon.Text = text, null);
    }

    private void BeginBusy(string text)
    {
        _busy = true;
        StartActivityIndicator();
        SetNotifyText(text);
    }

    private void EndBusy()
    {
        _busy = false;
        StopActivityIndicator();
        SetNotifyText("TextCrate");
    }

    private void StartActivityIndicator()
    {
        _uiContext.Send(_ =>
        {
            _activityDotVisible = true;
            SetActivityIcon(true);
            _activityTimer.Start();
        }, null);
    }

    private void StopActivityIndicator()
    {
        _uiContext.Send(_ =>
        {
            _activityTimer.Stop();
            _activityDotVisible = false;
            _notifyIcon.Icon = _normalIcon;
            _activityIcon?.Dispose();
            _activityIcon = null;
        }, null);
    }

    private void ToggleActivityIcon()
    {
        _activityDotVisible = !_activityDotVisible;
        SetActivityIcon(_activityDotVisible);
    }

    private void SetActivityIcon(bool showDot)
    {
        if (!showDot)
        {
            _notifyIcon.Icon = _normalIcon;
            return;
        }

        _activityIcon?.Dispose();
        _activityIcon = CreateActivityIcon();
        _notifyIcon.Icon = _activityIcon;
    }

    private Icon CreateActivityIcon()
    {
        var size = SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            using var baseBitmap = _normalIcon.ToBitmap();
            graphics.DrawImage(baseBitmap, new Rectangle(Point.Empty, size));
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var diameter = Math.Max(7, size.Width / 2);
            var padding = Math.Max(1, size.Width / 14);
            var x = padding;
            var y = size.Height - diameter - padding;
            using var shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            using var fill = new SolidBrush(Color.FromArgb(236, 72, 153));
            graphics.FillEllipse(shadow, x - 1, y + 1, diameter + 1, diameter + 1);
            graphics.FillEllipse(fill, x, y, diameter, diameter);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private void Exit()
    {
        CancelTyping();
        _hotKeyManager.Unregister();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _typingCancellation.Dispose();
            _activityTimer.Dispose();
            _hotKeyManager.Dispose();
            _activityIcon?.Dispose();
            _normalIcon.Dispose();
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
