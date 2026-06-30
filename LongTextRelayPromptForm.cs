namespace TextCrate;

internal sealed class LongTextRelayPromptForm : Form
{
    private readonly AppSettings _settings;
    private readonly CheckBox _burnAfterRead = new();
    private readonly ThemedSelect _expiry = new();
    private readonly CheckBox _usePassword = new();
    private readonly TextBox _password = new();

    public int ExpiryMinutes => (int)_expiry.SelectedValue!;
    public bool BurnAfterRead => _burnAfterRead.Checked;
    public string? Password => _usePassword.Checked ? _password.Text : null;

    public LongTextRelayPromptForm(AppSettings settings, int characterCount)
    {
        _settings = settings;
        Text = "TextCrate Long Text Relay";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "icon.ico"));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 360);
        Font = new Font("Segoe UI", 9F);
        TopMost = true;
        ShowInTaskbar = true;

        BuildLayout(characterCount);
        ApplyTheme();
    }

    private void BuildLayout(int characterCount)
    {
        Controls.Add(new Label
        {
            Text = "Use Long Text Relay?",
            Font = new Font("Segoe UI Semibold", 14F),
            AutoSize = true,
            Location = new Point(22, 20)
        });

        Controls.Add(new Label
        {
            Text = $"This clipboard has {characterCount:N0} characters. TextCrate can upload encrypted ciphertext to your configured Cloudflare backend and type only the one-time URL into the remote session.",
            AutoSize = false,
            Location = new Point(24, 58),
            Size = new Size(500, 58)
        });

        Controls.Add(new Label
        {
            Text = "Cloudflare never receives plaintext. Anyone with the full URL fragment and password, if set, can decrypt until the link expires or burns.",
            AutoSize = false,
            Location = new Point(24, 118),
            Size = new Size(500, 50)
        });

        _expiry.SetOptions(
            new SelectOption("1 minute", 1),
            new SelectOption("5 minutes", 5),
            new SelectOption("15 minutes", 15),
            new SelectOption("30 minutes", 30),
            new SelectOption("1 hour", 60));
        _expiry.SelectedValue = Math.Clamp((int)_settings.LongTextRelayExpiryMinutes, 1, 60);
        AddRow("Expiry", _expiry, 178);

        _burnAfterRead.Text = "Burn after first successful browser decrypt";
        _burnAfterRead.Checked = _settings.LongTextRelayBurnAfterRead;
        _burnAfterRead.AutoSize = true;
        _burnAfterRead.Location = new Point(148, 218);
        Controls.Add(_burnAfterRead);

        _usePassword.Text = "Require a password in the browser";
        _usePassword.Checked = _settings.LongTextRelayPromptForPassword;
        _usePassword.AutoSize = true;
        _usePassword.Location = new Point(148, 248);
        _usePassword.CheckedChanged += (_, _) => _password.Enabled = _usePassword.Checked;
        Controls.Add(_usePassword);

        _password.UseSystemPasswordChar = true;
        _password.Enabled = _usePassword.Checked;
        AddRow("Password", _password, 278);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(282, 316),
            Size = new Size(252, 36)
        };
        buttons.Controls.Add(CreateButton("Upload", true));
        buttons.Controls.Add(CreateButton("Type normally", false));
        Controls.Add(buttons);
    }

    private void AddRow(string label, Control control, int y)
    {
        Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Location = new Point(24, y + 5)
        });
        control.Location = new Point(148, y);
        control.Size = new Size(360, 28);
        Controls.Add(control);
    }

    private Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            DialogResult = primary ? DialogResult.OK : DialogResult.Cancel,
            Width = 116,
            Height = 32,
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat
        };
        if (primary)
        {
            AcceptButton = button;
        }
        else
        {
            CancelButton = button;
        }

        return button;
    }

    private void ApplyTheme()
    {
        var palette = ThemeService.GetPalette(_settings);
        ThemeService.ApplyToControl(this, palette);
        foreach (var label in Controls.OfType<Label>())
        {
            label.ForeColor = label.Font.Size >= 10 ? palette.Text : palette.MutedText;
        }

        foreach (var checkBox in Controls.OfType<CheckBox>())
        {
            checkBox.ForeColor = palette.Text;
        }

        _expiry.SetTheme(palette);
        foreach (var button in Controls.OfType<FlowLayoutPanel>().SelectMany(static p => p.Controls.OfType<Button>()))
        {
            var primary = button.DialogResult == DialogResult.OK;
            button.BackColor = primary ? palette.Accent : palette.Surface;
            button.ForeColor = primary ? palette.AccentText : palette.Text;
            button.FlatAppearance.BorderColor = primary ? palette.Accent : palette.Border;
        }
    }
}
