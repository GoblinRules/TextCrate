using Microsoft.Win32;

namespace TextCrate;

internal sealed record ThemePalette(
    bool IsDark,
    Color Window,
    Color Surface,
    Color SurfaceAlt,
    Color Text,
    Color MutedText,
    Color Border,
    Color Accent,
    Color AccentText);

internal static class ThemeService
{
    public static ThemePalette GetPalette(AppSettings settings)
    {
        var dark = settings.Theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDark()
        };

        return dark
            ? new ThemePalette(
                true,
                Color.FromArgb(15, 23, 42),
                Color.FromArgb(30, 41, 59),
                Color.FromArgb(51, 65, 85),
                Color.FromArgb(241, 245, 249),
                Color.FromArgb(148, 163, 184),
                Color.FromArgb(71, 85, 105),
                Color.FromArgb(34, 211, 238),
                Color.FromArgb(8, 47, 73))
            : new ThemePalette(
                false,
                Color.FromArgb(248, 250, 252),
                Color.White,
                Color.FromArgb(241, 245, 249),
                Color.FromArgb(15, 23, 42),
                Color.FromArgb(71, 85, 105),
                Color.FromArgb(203, 213, 225),
                Color.FromArgb(14, 116, 144),
                Color.White);
    }

    private static bool IsSystemDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    public static void ApplyToControl(Control control, ThemePalette palette)
    {
        control.BackColor = palette.Window;
        control.ForeColor = palette.Text;

        foreach (Control child in control.Controls)
        {
            ApplyChildTheme(child, palette);
        }
    }

    private static void ApplyChildTheme(Control control, ThemePalette palette)
    {
        switch (control)
        {
            case Button button:
                button.BackColor = palette.Surface;
                button.ForeColor = palette.Text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = palette.Border;
                break;
            case ComboBox:
            case NumericUpDown:
            case TextBox:
                control.BackColor = palette.Surface;
                control.ForeColor = palette.Text;
                break;
            case TableLayoutPanel:
            case FlowLayoutPanel:
            case Panel:
            case GroupBox:
                control.BackColor = palette.Window;
                control.ForeColor = palette.Text;
                break;
            case CheckBox:
            case Label:
                control.BackColor = palette.Window;
                control.ForeColor = palette.Text;
                break;
            default:
                control.BackColor = palette.Window;
                control.ForeColor = palette.Text;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyChildTheme(child, palette);
        }
    }
}

internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly ThemePalette _palette;

    public ThemedMenuRenderer(ThemePalette palette) : base(new ThemedMenuColorTable(palette))
    {
        _palette = palette;
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(_palette.Border);
        e.Graphics.DrawRectangle(pen, new Rectangle(Point.Empty, new Size(e.ToolStrip.Width - 1, e.ToolStrip.Height - 1)));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = _palette.Text;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = _palette.Text;
        base.OnRenderArrow(e);
    }
}

internal sealed class ThemedMenuColorTable : ProfessionalColorTable
{
    private readonly ThemePalette _palette;

    public ThemedMenuColorTable(ThemePalette palette)
    {
        _palette = palette;
    }

    public override Color ToolStripDropDownBackground => _palette.Surface;
    public override Color ImageMarginGradientBegin => _palette.Surface;
    public override Color ImageMarginGradientMiddle => _palette.Surface;
    public override Color ImageMarginGradientEnd => _palette.Surface;
    public override Color MenuBorder => _palette.Border;
    public override Color MenuItemBorder => _palette.Accent;
    public override Color MenuItemSelected => _palette.SurfaceAlt;
    public override Color MenuItemSelectedGradientBegin => _palette.SurfaceAlt;
    public override Color MenuItemSelectedGradientEnd => _palette.SurfaceAlt;
    public override Color SeparatorDark => _palette.Border;
    public override Color SeparatorLight => _palette.Border;
}
