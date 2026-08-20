using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace AntiPilot.UI.Fluent;

/// <summary>
/// The Windows 11 toggle switch.
///
/// A checkbox and a toggle say different things: a checkbox is one of several choices you are
/// making before pressing Save, a toggle is a switch that takes effect as you flip it. The tray
/// icon and the double-press setting are the second kind, which is why they get this.
/// </summary>
internal sealed class ToggleSwitch : Control, IThemedControl
{
    private bool _checked;
    private bool _hover;
    private bool _pressed;

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent;
        Size = new Size(40, 20);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sets the state without raising <see cref="CheckedChanged"/>, for loading settings in.</summary>
    public void SetCheckedQuietly(bool value)
    {
        _checked = value;
        Invalidate();
    }

    public void OnThemeChanged() => Invalidate();

    protected override Size DefaultSize => new(40, 20);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int height = FluentPaint.Dpi(this, 20);
        int width = FluentPaint.Dpi(this, 40);
        var track = new Rectangle(0, (Height - height) / 2, width - 1, height - 1);
        int radius = track.Height / 2;

        Color fill, stroke, knob;

        if (!Enabled)
        {
            fill = _checked ? Theme.DisabledText : Color.Transparent;
            stroke = Theme.DisabledText;
            knob = _checked ? Theme.Card : Theme.DisabledText;
        }
        else if (_checked)
        {
            fill = _pressed ? Theme.AccentPressed : _hover ? Theme.AccentHover : Theme.Accent;
            stroke = fill;
            knob = Theme.AccentText;
        }
        else
        {
            fill = _pressed ? Theme.SubtlePressed : _hover ? Theme.SubtleHover : Theme.ControlFill;
            stroke = Theme.SecondaryText;
            knob = Theme.Text;
        }

        using (var path = FluentPaint.RoundedRect(track, radius))
        {
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);

            using var pen = new Pen(stroke);
            g.DrawPath(pen, path);
        }

        // The knob grows a little while pressed, which is the small piece of motion the real
        // control has and the thing that makes it feel like a switch rather than a picture of one.
        int inset = FluentPaint.Dpi(this, 3);
        int size = track.Height - inset * 2 + (_pressed ? FluentPaint.Dpi(this, 2) : 0);
        int y = track.Y + (track.Height - size) / 2;
        // "On" is the trailing edge, which mirrors with the language.
        bool atEnd = FluentPaint.Rtl ? !_checked : _checked;
        int x = atEnd ? track.Right - size - inset : track.X + inset;

        using (var brush = new SolidBrush(knob))
        {
            g.FillEllipse(brush, x, y, size, size);
        }

        if (Focused)
        {
            var focus = new Rectangle(track.X - 2, track.Y - 2, track.Width + 4, track.Height + 4);
            using var path = FluentPaint.RoundedRect(focus, radius + 2);
            using var pen = new Pen(Theme.Text, 2);
            g.DrawPath(pen, path);
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
            if (ClientRectangle.Contains(e.Location))
            {
                Checked = !Checked;
            }

            Invalidate();
        }

        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
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
