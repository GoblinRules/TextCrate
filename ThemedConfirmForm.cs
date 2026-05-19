using System.Runtime.InteropServices;

namespace TextCrate;

internal sealed class ThemedConfirmForm : Form
{
    private readonly AppSettings _settings;
    private readonly int _characterCount;
    private readonly string _targetName;

    public ThemedConfirmForm(AppSettings settings, int characterCount, string targetName)
    {
        _settings = settings;
        _characterCount = characterCount;
        _targetName = string.IsNullOrWhiteSpace(targetName) ? "selected target" : targetName;

        Text = "TextCrate Confirm Typing";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "icon.ico"));
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(500, 230);

        BuildLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
        SetForegroundWindow(Handle);
        FlashWindow(Handle, invert: true);
    }

    private void BuildLayout()
    {
        var palette = ThemeService.GetPalette(_settings);
        BackColor = palette.Window;
        ForeColor = palette.Text;

        var title = new Label
        {
            Text = "Confirm Typing",
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = palette.Text,
            Location = new Point(24, 22),
            Size = new Size(430, 34)
        };

        var target = _targetName.Length > 70 ? _targetName[..67] + "..." : _targetName;
        var body = new Label
        {
            Text = $"Type {_characterCount:N0} characters into {target}?",
            ForeColor = palette.Text,
            Location = new Point(82, 82),
            Size = new Size(378, 42)
        };

        var warning = new Panel
        {
            Location = new Point(28, 78),
            Size = new Size(38, 38),
            BackColor = palette.Accent
        };
        warning.Paint += (_, e) =>
        {
            using var font = new Font("Segoe UI Semibold", 20F);
            TextRenderer.DrawText(e.Graphics, "!", font, warning.ClientRectangle, palette.AccentText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        var hint = new Label
        {
            Text = "TextCrate will restore focus to the clicked target before typing.",
            ForeColor = palette.MutedText,
            Location = new Point(82, 122),
            Size = new Size(378, 28)
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(24, 170),
            Size = new Size(452, 38),
            BackColor = Color.Transparent
        };

        var no = CreateButton("No", false, palette);
        var yes = CreateButton("Yes", true, palette);
        buttons.Controls.Add(no);
        buttons.Controls.Add(yes);

        AcceptButton = yes;
        CancelButton = no;
        Controls.Add(title);
        Controls.Add(warning);
        Controls.Add(body);
        Controls.Add(hint);
        Controls.Add(buttons);
    }

    private static Button CreateButton(string text, bool primary, ThemePalette palette)
    {
        return new Button
        {
            Text = text,
            DialogResult = primary ? DialogResult.Yes : DialogResult.No,
            Width = 104,
            Height = 34,
            Margin = new Padding(8, 0, 0, 0),
            BackColor = primary ? palette.Accent : palette.Surface,
            ForeColor = primary ? palette.AccentText : palette.Text,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool FlashWindow(IntPtr windowHandle, bool invert);
}
