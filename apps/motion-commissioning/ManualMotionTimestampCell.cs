namespace Wjss.MotionCommissioning;

/// <summary>
/// A presentation-only cell for one rapidly changing observation timestamp in the Manual Motion
/// Commissioning state grid.
///
/// The cell is a directly user-painted control rather than a standard Label. Its background and
/// complete current text are drawn in one WM_PAINT operation into the WinForms optimized paint
/// buffer for this small client area. The grid assigns a new string only when the displayed text
/// changes, and that change invalidates this cell. This keeps timestamp presentation confined to
/// the timestamp cell without changing the timestamp,
/// freshness, render cadence, session, or motion semantics supplied by the grid.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build and review are the validation of record).
/// </summary>
internal sealed class ManualMotionTimestampCell : Control
{
    private const TextFormatFlags TimestampTextFlags =
        TextFormatFlags.Left |
        TextFormatFlags.VerticalCenter |
        TextFormatFlags.WordBreak |
        TextFormatFlags.TextBoxControl |
        TextFormatFlags.NoPrefix |
        TextFormatFlags.PreserveGraphicsClipping |
        TextFormatFlags.PreserveGraphicsTranslateTransform;

    private SolidBrush? _backgroundBrush;

    internal ManualMotionTimestampCell()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, false);

        AccessibleName = string.Empty;
        AccessibleRole = AccessibleRole.StaticText;
        BackColor = SystemColors.Control;
        TabStop = false;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        AccessibleName = Text;
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        e.Graphics.FillRectangle(BackgroundBrush, ClientRectangle);

        var text = Text;
        var textBounds = new Rectangle(
            Padding.Left,
            Padding.Top,
            Math.Max(0, ClientSize.Width - Padding.Horizontal),
            Math.Max(0, ClientSize.Height - Padding.Vertical));
        if (text.Length != 0 && textBounds.Width != 0 && textBounds.Height != 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                text,
                Font,
                textBounds,
                Enabled ? ForeColor : SystemColors.GrayText,
                TimestampTextFlags);
        }

        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backgroundBrush?.Dispose();
            _backgroundBrush = null;
        }

        base.Dispose(disposing);
    }

    private SolidBrush BackgroundBrush
    {
        get
        {
            var brush = _backgroundBrush;
            if (brush is null || brush.Color != BackColor)
            {
                brush?.Dispose();
                brush = new SolidBrush(BackColor);
                _backgroundBrush = brush;
            }

            return brush;
        }
    }
}
