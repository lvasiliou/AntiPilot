using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace AntiPilot.UI.Fluent;

/// <summary>
/// The left-hand navigation of a Windows 11 settings window: an icon and a label per page, the
/// selected one carrying a short accent-coloured pill against its leading edge.
///
/// Tabs would have been less work, but tab strips have looked like 2005 for twenty years, and this
/// is also the layout that copes with a fifth page being added later.
/// </summary>
internal sealed class NavigationRail : Control, IThemedControl
{
    private readonly List<(string Glyph, string Label)> _items = [];
    private int _selected;
    private int _hovered = -1;

    public event EventHandler? SelectedIndexChanged;

    public NavigationRail()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent;
        Font = Typography.Body;
        TabStop = true;
        Width = 184;
    }

    public int ItemHeight => FluentPaint.Dpi(this, 40);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, _items.Count - 1));
            if (_selected == clamped)
            {
                return;
            }

            _selected = clamped;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Add(string glyph, string label)
    {
        _items.Add((glyph, label));
        Invalidate();
    }

    public void OnThemeChanged() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int height = ItemHeight;
        int gap = FluentPaint.Dpi(this, 4);
        int radius = FluentPaint.Dpi(this, Theme.ControlRadius);

        for (int i = 0; i < _items.Count; i++)
        {
            var (glyph, label) = _items[i];
            var bounds = new Rectangle(0, i * (height + gap), Width, height);

            bool selected = i == _selected;
            bool hovered = i == _hovered;

            if (selected || hovered)
            {
                var fill = selected ? Theme.SubtleHover : Theme.Blend(Theme.SubtleHover, Theme.Window, 0.4);
                FluentPaint.DrawSurface(g, bounds, radius, fill);
            }

            if (selected)
            {
                // The accent pill. Three pixels wide and about half the row tall, exactly as in
                // the shell's own navigation, and on the leading edge whichever way that runs.
                int pillHeight = height / 2;
                var pill = FluentPaint.Mirror(
                    new Rectangle(
                        FluentPaint.Dpi(this, 2),
                        bounds.Y + (height - pillHeight) / 2,
                        FluentPaint.Dpi(this, 3),
                        pillHeight),
                    Width);

                using var path = FluentPaint.RoundedRect(pill, pill.Width / 2);
                using var brush = new SolidBrush(Theme.Accent);
                g.FillPath(brush, path);
            }

            int x = FluentPaint.Dpi(this, 14);
            int iconWidth = FluentPaint.Dpi(this, 26);

            TextRenderer.DrawText(g, glyph, Typography.SmallIcon,
                FluentPaint.Mirror(new Rectangle(x, bounds.Y, iconWidth, height), Width), Theme.Text,
                FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter));

            TextRenderer.DrawText(g, label, selected ? Typography.BodyStrong : Typography.Body,
                FluentPaint.Mirror(new Rectangle(x + iconWidth, bounds.Y, Width - x - iconWidth - FluentPaint.Dpi(this, 8), height), Width),
                Theme.Text, FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter));
        }

        if (Focused && _items.Count > 0)
        {
            var bounds = new Rectangle(0, _selected * (height + gap), Width - 1, height - 1);
            using var path = FluentPaint.RoundedRect(bounds, radius);
            using var pen = new Pen(Theme.Text, 1.5f);
            g.DrawPath(pen, path);
        }
    }

    private int IndexAt(Point point)
    {
        int height = ItemHeight + FluentPaint.Dpi(this, 4);
        int index = point.Y / height;
        return index >= 0 && index < _items.Count ? index : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int index = IndexAt(e.Location);
        if (index != _hovered)
        {
            _hovered = index;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int index = IndexAt(e.Location);
        if (index >= 0)
        {
            Focus();
            SelectedIndex = index;
        }

        base.OnMouseDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Down:
                SelectedIndex = Math.Min(_selected + 1, _items.Count - 1);
                e.Handled = true;
                break;

            case Keys.Up:
                SelectedIndex = Math.Max(_selected - 1, 0);
                e.Handled = true;
                break;

            case Keys.Home:
                SelectedIndex = 0;
                e.Handled = true;
                break;

            case Keys.End:
                SelectedIndex = _items.Count - 1;
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

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
