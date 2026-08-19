using System.ComponentModel;

namespace AntiPilot.UI.Fluent;

/// <summary>
/// A Windows 11 button: 4px corners, a one-pixel outline, and an accent-filled variant for the one
/// button on a window that is the point of it.
/// </summary>
internal sealed class FluentButton : Control, IThemedControl
{
    private bool _hover;
    private bool _pressed;

    public FluentButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent;
        Font = Typography.Body;
        Padding = new Padding(16, 6, 16, 6);
        TabStop = true;
        Cursor = Cursors.Hand;
        AutoSize = false;
    }

    /// <summary>Filled with the user's accent colour. At most one per window, by convention.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsAccent { get; set; }

    /// <summary>An optional icon-font glyph shown before the text.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Glyph { get; set; }

    public void OnThemeChanged() => Invalidate();

    /// <summary>Width that fits the text and padding, so callers do not have to guess.</summary>
    public int PreferredWidth
    {
        get
        {
            using var g = CreateGraphics();
            var size = TextRenderer.MeasureText(g, Text, Font, new Size(int.MaxValue, int.MaxValue), FluentPaint.Text);
            int glyph = string.IsNullOrEmpty(Glyph) ? 0 : FluentPaint.Dpi(this, 22);
            return size.Width + glyph + Padding.Horizontal;
        }
    }

    protected override Size DefaultSize => new(120, 32);

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        int radius = FluentPaint.Dpi(this, Theme.ControlRadius);

        Color fill, stroke, text;

        if (!Enabled)
        {
            fill = Theme.IsDark ? Theme.SubtlePressed : Theme.SubtleHover;
            stroke = Theme.ControlStroke;
            text = Theme.DisabledText;
        }
        else if (IsAccent)
        {
            fill = _pressed ? Theme.AccentPressed : _hover ? Theme.AccentHover : Theme.Accent;
            stroke = fill;
            text = Theme.AccentText;
        }
        else
        {
            fill = _pressed ? Theme.SubtlePressed : _hover ? Theme.SubtleHover : Theme.ControlFill;
            stroke = Theme.ControlStroke;
            text = Theme.Text;
        }

        FluentPaint.DrawSurface(e.Graphics, bounds, radius, fill, stroke);

        var content = bounds;

        if (!string.IsNullOrEmpty(Glyph))
        {
            int glyphWidth = FluentPaint.Dpi(this, 22);
            var glyphBounds = new Rectangle(bounds.X + Padding.Left, bounds.Y, glyphWidth, bounds.Height);
            TextRenderer.DrawText(e.Graphics, Glyph, Typography.SmallIcon, FluentPaint.Mirror(glyphBounds, bounds.Width), text,
                FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter));
            content = new Rectangle(glyphBounds.Right, bounds.Y, bounds.Right - glyphBounds.Right - Padding.Right, bounds.Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, FluentPaint.Mirror(content, bounds.Width), text,
                FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter));
        }
        else
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, content, text,
                FluentPaint.Rtl ? FluentPaint.TextCentre | TextFormatFlags.RightToLeft : FluentPaint.TextCentre);
        }

        if (Focused && Enabled)
        {
            var focus = Rectangle.Inflate(bounds, -2, -2);
            using var path = FluentPaint.RoundedRect(new Rectangle(focus.X, focus.Y, focus.Width - 1, focus.Height - 1), radius);
            using var pen = new Pen(IsAccent ? Theme.AccentText : Theme.Text, 1.5f);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Focus();
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_pressed && e.Button == MouseButtons.Left)
        {
            _pressed = false;
            Invalidate();

            if (ClientRectangle.Contains(e.Location))
            {
                OnClick(EventArgs.Empty);
            }
        }

        base.OnMouseUp(e);
    }

    // The base implementation would raise Click a second time for the same press.
    protected override void OnMouseClick(MouseEventArgs e)
    {
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }
}
