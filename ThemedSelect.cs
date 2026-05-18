using System.ComponentModel;

namespace TextCrate;

internal sealed class ThemedSelect : Control
{
    private readonly ContextMenuStrip _menu = new();
    private readonly List<SelectOption> _options = [];
    private ThemePalette _palette = ThemeService.GetPalette(new AppSettings());
    private SelectOption? _selected;

    public event EventHandler? SelectedValueChanged;

    public ThemedSelect()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand;
        Height = 28;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedValue
    {
        get => _selected?.Value;
        set
        {
            _selected = _options.FirstOrDefault(option => Equals(option.Value, value));
            Invalidate();
            SelectedValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string Text
    {
        get => _selected?.Label ?? string.Empty;
        set { }
    }

    public void SetOptions(params SelectOption[] options)
    {
        _options.Clear();
        _options.AddRange(options);
        _selected ??= _options.FirstOrDefault();
        BuildMenu();
        Invalidate();
    }

    public void SetTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = palette.Surface;
        ForeColor = palette.Text;
        _menu.BackColor = palette.Surface;
        _menu.ForeColor = palette.Text;
        _menu.Renderer = new ThemedMenuRenderer(palette);
        BuildMenu();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var background = new SolidBrush(_palette.Surface);
        using var border = new Pen(_palette.Border);
        e.Graphics.FillRectangle(background, ClientRectangle);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        var arrowArea = new Rectangle(Width - 28, 0, 28, Height);
        using var arrowBackground = new SolidBrush(_palette.SurfaceAlt);
        e.Graphics.FillRectangle(arrowBackground, arrowArea);
        e.Graphics.DrawLine(border, arrowArea.Left, 0, arrowArea.Left, Height);

        var textRect = new Rectangle(8, 0, Width - 40, Height);
        TextRenderer.DrawText(e.Graphics, _selected?.Label ?? string.Empty, Font, textRect, _palette.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var midX = arrowArea.Left + (arrowArea.Width / 2);
        var midY = arrowArea.Top + (arrowArea.Height / 2) + 1;
        using var arrow = new SolidBrush(_palette.Text);
        e.Graphics.FillPolygon(arrow, [new Point(midX - 4, midY - 2), new Point(midX + 4, midY - 2), new Point(midX, midY + 3)]);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _menu.Show(this, new Point(0, Height));
        }
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();
        foreach (var option in _options)
        {
            var item = new ToolStripMenuItem(option.Label)
            {
                Checked = Equals(option.Value, _selected?.Value),
                Tag = option
            };
            item.Click += (_, _) =>
            {
                _selected = option;
                SelectedValueChanged?.Invoke(this, EventArgs.Empty);
                BuildMenu();
                Invalidate();
            };
            _menu.Items.Add(item);
        }
    }
}

internal sealed record SelectOption(string Label, object Value);
