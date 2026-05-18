namespace TextCrate;

internal sealed class RegionSelectionForm : Form
{
    private Point? _startPoint;
    private Point? _currentPoint;

    public Rectangle? SelectedRegion { get; private set; }

    public RegionSelectionForm()
    {
        Bounds = SystemInformation.VirtualScreen;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        Opacity = 0.3;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Black;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _startPoint = e.Location;
            _currentPoint = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_startPoint.HasValue)
        {
            _currentPoint = e.Location;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_startPoint.HasValue)
        {
            return;
        }

        var local = GetSelectionRectangle(_startPoint.Value, e.Location);
        if (local.Width < 5 || local.Height < 5)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        SelectedRegion = new Rectangle(PointToScreen(local.Location), local.Size);
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_startPoint.HasValue && _currentPoint.HasValue)
        {
            var rectangle = GetSelectionRectangle(_startPoint.Value, _currentPoint.Value);
            using var pen = new Pen(Color.DeepSkyBlue, 3);
            using var brush = new SolidBrush(Color.FromArgb(80, Color.DeepSkyBlue));
            e.Graphics.FillRectangle(brush, rectangle);
            e.Graphics.DrawRectangle(pen, rectangle);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private static Rectangle GetSelectionRectangle(Point first, Point second)
    {
        var x = Math.Min(first.X, second.X);
        var y = Math.Min(first.Y, second.Y);
        var width = Math.Abs(first.X - second.X);
        var height = Math.Abs(first.Y - second.Y);
        return new Rectangle(x, y, width, height);
    }
}
