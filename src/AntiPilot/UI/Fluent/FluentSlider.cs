using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace AntiPilot.UI.Fluent;

/// <summary>
/// The Windows 11 slider: a thin track, the filled part in the accent colour, and a ring-shaped
/// thumb whose centre grows on hover. A TrackBar would have done the job, but it is drawn by the
/// common controls library and looks its age next to everything else here.
/// </summary>
internal sealed class FluentSlider : Control, IThemedControl
{
    private int _value;
    private bool _hover;
    private bool _dragging;

    public event EventHandler? ValueChanged;

    public FluentSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent;
        Size = new Size(220, 32);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get; set; } = 100;

    /// <summary>Values snap to this, so the number under the slider is one a person would say.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Step { get; set; } = 1;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int snapped = Snap(value);
            if (_value == snapped)
            {
                return;
            }

            _value = snapped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sets the value without raising <see cref="ValueChanged"/>, for loading settings in.</summary>
    public void SetValueQuietly(int value)
    {
        _value = Snap(value);
        Invalidate();
    }

    public void OnThemeChanged() => Invalidate();

    private int Snap(int value)
    {
        value = Math.Clamp(value, Minimum, Maximum);

        if (Step > 1)
        {
            value = Minimum + (int)Math.Round((value - Minimum) / (double)Step) * Step;
        }

        return Math.Clamp(value, Minimum, Maximum);
    }

    private int ThumbRadius => FluentPaint.Dpi(this, 10);

    private Rectangle TrackBounds
    {
        get
        {
            int height = FluentPaint.Dpi(this, 4);
            int inset = ThumbRadius;
            return new Rectangle(inset, (Height - height) / 2, Math.Max(1, Width - inset * 2), height);
        }
    }

    private int ThumbCentre
    {
        get
        {
            var track = TrackBounds;
            double fraction = Maximum == Minimum ? 0 : (_value - Minimum) / (double)(Maximum - Minimum);
            return track.X + (int)Math.Round(fraction * track.Width);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = TrackBounds;
        int centre = ThumbCentre;
        int radius = track.Height / 2;

        bool on = Enabled;
        var railColour = on ? Theme.Blend(Theme.SecondaryText, Theme.Window, 0.55) : Theme.DisabledText;
        var fillColour = on ? Theme.Accent : Theme.DisabledText;

        using (var path = FluentPaint.RoundedRect(track, radius))
        using (var brush = new SolidBrush(railColour))
        {
            g.FillPath(brush, path);
        }

        var filled = track with { Width = Math.Max(1, centre - track.X) };
        using (var path = FluentPaint.RoundedRect(filled, radius))
        using (var brush = new SolidBrush(fillColour))
        {
            g.FillPath(brush, path);
        }

        // The thumb is an accent ring with a themed centre; the centre grows on hover and shrinks
        // while dragging, which is the whole of the control's animation vocabulary.
        int outer = ThumbRadius;
        int inner = _dragging ? FluentPaint.Dpi(this, 4) : _hover ? FluentPaint.Dpi(this, 7) : FluentPaint.Dpi(this, 6);
        int y = Height / 2;

        using (var brush = new SolidBrush(on ? Theme.Accent : Theme.DisabledText))
        {
            g.FillEllipse(brush, centre - outer, y - outer, outer * 2, outer * 2);
        }

        using (var brush = new SolidBrush(Theme.IsDark ? Color.FromArgb(0x45, 0x45, 0x45) : Color.White))
        {
            g.FillEllipse(brush, centre - inner, y - inner, inner * 2, inner * 2);
        }

        if (Focused)
        {
            using var pen = new Pen(Theme.Text, 1.5f);
            g.DrawEllipse(pen, centre - outer - 2, y - outer - 2, (outer + 2) * 2, (outer + 2) * 2);
        }
    }

    private void MoveTo(int x)
    {
        var track = TrackBounds;
        double fraction = (x - track.X) / (double)Math.Max(1, track.Width);
        Value = (int)Math.Round(Minimum + fraction * (Maximum - Minimum));
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
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled)
        {
            _dragging = true;
            Focus();
            MoveTo(e.X);
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            MoveTo(e.X);
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down:
                Value -= Math.Max(1, Step);
                e.Handled = true;
                break;

            case Keys.Right or Keys.Up:
                Value += Math.Max(1, Step);
                e.Handled = true;
                break;

            case Keys.Home:
                Value = Minimum;
                e.Handled = true;
                break;

            case Keys.End:
                Value = Maximum;
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

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
