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

        Padding = new Padding(16, 10, 16, 10);
        Margin = new Padding(0, 0, 0, Theme.CardGap);
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        AccessibleRole = AccessibleRole.Grouping;
    }

    /// <summary>
    /// Height the card needs so its text fits at <paramref name="width"/>.
    ///
    /// The card used to be told its height, which is why a long line in a translated build was cut
    /// off rather than wrapped, and why it got worse the further the display was scaled: the text
    /// grew with the DPI and the box did not.
    /// </summary>
    public int MeasuredHeight(int width)
    {
        int textWidth = TextWidth(width);
        int text = FluentPaint.WrappedHeight(_title, Typography.Body, textWidth);

        if (!string.IsNullOrEmpty(_description))
        {
            text += FluentPaint.Dpi(this, 2) + FluentPaint.WrappedHeight(_description, Typography.Caption, textWidth);
        }

        int content = Math.Max(text, _action?.Height ?? 0);
        return Math.Max(Padding.Top + content + Padding.Bottom, FluentPaint.Dpi(this, 40));
    }

    /// <summary>Re-measures against the current width. Safe to call repeatedly.</summary>
    public void RefreshHeight()
    {
        int wanted = MeasuredHeight(Width);
        if (Height != wanted)
        {
            Height = wanted;
        }
    }

    /// <summary>Where the text starts: past the icon when there is one.</summary>
    private int TextLeft() => Padding.Left +
        (string.IsNullOrEmpty(_glyph) ? 0 : FluentPaint.Dpi(this, 28) + FluentPaint.Dpi(this, 12));

    /// <summary>Room the text has, once the icon and the action on the far side are taken out.</summary>
    private int TextWidth(int width) => width - TextLeft() - Padding.Right -
        (_action is null ? 0 : _action.Width + FluentPaint.Dpi(this, 16));

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
        set { _glyph = value; RefreshHeight(); Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => _title;
        set { _title = value; AccessibleName = value; NameAction(); RefreshHeight(); Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Description
    {
        get => _description;
        set { _description = value; AccessibleDescription = value; RefreshHeight(); Invalidate(); }
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
                NameAction();
                LayoutAction();
            }
        }
    }

    /// <summary>
    /// Lends the card's title to the control on the right. A toggle or a slider has no text of its
    /// own, so without this a screen reader announces the row and then an unnamed switch.
    ///
    /// A button is left alone: its own label is the more useful thing to hear, and borrowing the
    /// title made "Open Windows settings" announce itself as the sentence above it.
    /// </summary>
    private void NameAction()
    {
        if (_action is not null && string.IsNullOrEmpty(_action.AccessibleName) && string.IsNullOrEmpty(_action.Text))
        {
            _action.AccessibleName = _title;
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
        RefreshHeight();
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
        if (!string.IsNullOrEmpty(_glyph))
        {
            var iconBounds = FluentPaint.Mirror(new Rectangle(Padding.Left, 0, FluentPaint.Dpi(this, 28), Height), Width);
            TextRenderer.DrawText(g, _glyph, Typography.Icon, iconBounds, Theme.Text,
                FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter));
        }

        int x = TextLeft();
        int textWidth = TextWidth(Width);

        if (textWidth <= 0)
        {
            return;
        }

        // The same measurement the card sized itself by, so what was budgeted is what gets drawn.
        var flags = FluentPaint.Leading(FluentPaint.TextWrap);
        int titleHeight = FluentPaint.WrappedHeight(_title, Typography.Body, textWidth);
        int descriptionHeight = FluentPaint.WrappedHeight(_description, Typography.Caption, textWidth);
        int gap = descriptionHeight == 0 ? 0 : FluentPaint.Dpi(this, 2);
        int top = (Height - (titleHeight + gap + descriptionHeight)) / 2;

        TextRenderer.DrawText(g, _title, Typography.Body,
            FluentPaint.Mirror(new Rectangle(x, top, textWidth, titleHeight), Width), Theme.Text, flags);

        if (descriptionHeight > 0)
        {
            TextRenderer.DrawText(g, _description, Typography.Caption,
                FluentPaint.Mirror(new Rectangle(x, top + titleHeight + gap, textWidth, descriptionHeight), Width),
                Theme.SecondaryText, flags);
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
