namespace TextCrate;

internal sealed class TargetPickerForm : Form
{
    public Point? TargetPoint { get; private set; }

    public TargetPickerForm()
    {
        Bounds = SystemInformation.VirtualScreen;
        Cursor = Cursors.Cross;
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        Opacity = 0.08;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Black;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        TargetPoint = PointToScreen(e.Location);
        DialogResult = DialogResult.OK;
        Close();
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
}
