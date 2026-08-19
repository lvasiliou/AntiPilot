using System.ComponentModel;

namespace AntiPilot.UI.Fluent;

/// <summary>
/// The row Windows 11 Settings is built out of: an icon, a title, a quieter line of description,
/// and the control that actually does something on the right.
///
/// Cards are stacked with a small gap and, in a group, the top and bottom ones round only their
/// outer corners — which is what makes a run of them read as one panel rather than as a pile of
/// separate boxes.
/// </summary>
internal class SettingsCard : Panel, IThemedControl
{
    private string _glyph = string.Empty;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private Control? _action;
    private bool _hover;

    public SettingsCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        Height = 60;
        Padding = new Padding(16, 10, 16, 10);
        Margin = new Padding(0, 0, 0, Theme.CardGap);
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
    }

    /// <summary>Which corners to round, so a group of cards looks like one surface.</summary>
    internal enum Position
    {
        /// <summary>The only card in its group: round all four.</summary>
        Only,

        Top,
        Middle,
        Bottom,
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Position Place { get; set; } = Position.Only;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Glyph
    {
        get => _glyph;
        set { _glyph = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => _title;
        set { _title = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Description
    {
        get => _description;
        set { _description = value; Invalidate(); }
    }

    /// <summary>Highlights the row under the pointer. Off for cards that are not themselves clickable.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Interactive { get; set; }

    /// <summary>The control on the right — a toggle, a button, a combo.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Action
    {
        get => _action;
        set
        {
            if (_action is not null)
            {
                Controls.Remove(_action);
            }

            _action = value;

            if (_action is not null)
            {
                Controls.Add(_action);
                LayoutAction();
            }
        }
    }

    public void OnThemeChanged()
    {
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        LayoutAction();
    }

    private void LayoutAction()
    {
        if (_action is null)
        {
            return;
        }

        // The action sits on the trailing edge, which is the left one in a right-to-left language.
        int x = FluentPaint.Rtl
            ? Padding.Left
            : Width - Padding.Right - _action.Width;

        _action.Location = new Point(x, (Height - _action.Height) / 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int radius = FluentPaint.Dpi(this, Theme.CardRadius);

        var fill = Interactive && _hover ? Theme.SubtleHover : Theme.Card;

        // Overdraw past the edge that meets the next card, so the two share a straight join and
        // only the outside of the group is rounded.
        var bounds = ClientRectangle;
        var surface = Place switch
        {
            Position.Top => bounds with { Height = bounds.Height + radius },
            Position.Bottom => bounds with { Y = bounds.Y - radius, Height = bounds.Height + radius },
            Position.Middle => bounds with { Y = bounds.Y - radius, Height = bounds.Height + radius * 2 },
            _ => bounds,
        };

        var clip = g.Clip;
        g.SetClip(bounds);
        FluentPaint.DrawSurface(g, surface, radius, fill, Theme.CardStroke);
        g.Clip = clip;

        // Everything below is laid out from the leading edge and mirrored at the end, so the
        // arithmetic only has to be right once.
        int x = Padding.Left;
        int iconWidth = FluentPaint.Dpi(this, 28);

        if (!string.IsNullOrEmpty(_glyph))
        {
            var iconBounds = FluentPaint.Mirror(new Rectangle(x, 0, iconWidth, Height), Width);
            TextRenderer.DrawText(g, _glyph, Typography.Icon, iconBounds, Theme.Text,
                FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter));
            x += iconWidth + FluentPaint.Dpi(this, 12);
        }

        int actionWidth = _action is null ? 0 : _action.Width + FluentPaint.Dpi(this, 16);
        int textWidth = Width - x - Padding.Right - actionWidth;

        if (textWidth <= 0)
        {
            return;
        }

        var titleFlags = FluentPaint.Leading(FluentPaint.Text);

        if (!string.IsNullOrEmpty(_description))
        {
            var titleSize = TextRenderer.MeasureText(g, _title, Typography.Body, new Size(textWidth, int.MaxValue), FluentPaint.Text);
            var descriptionSize = TextRenderer.MeasureText(g, _description, Typography.Caption, new Size(textWidth, int.MaxValue), FluentPaint.Text);

            int total = titleSize.Height + descriptionSize.Height + FluentPaint.Dpi(this, 2);
            int top = (Height - total) / 2;

            TextRenderer.DrawText(g, _title, Typography.Body,
                FluentPaint.Mirror(new Rectangle(x, top, textWidth, titleSize.Height), Width), Theme.Text, titleFlags);

            TextRenderer.DrawText(g, _description, Typography.Caption,
                FluentPaint.Mirror(new Rectangle(x, top + titleSize.Height + FluentPaint.Dpi(this, 2), textWidth, descriptionSize.Height), Width),
                Theme.SecondaryText, titleFlags);
        }
        else
        {
            TextRenderer.DrawText(g, _title, Typography.Body,
                FluentPaint.Mirror(new Rectangle(x, 0, textWidth, Height), Width), Theme.Text,
                FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter));
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        if (Interactive)
        {
            Invalidate();
        }

        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        if (Interactive)
        {
            Invalidate();
        }

        base.OnMouseLeave(e);
    }
}
