namespace AntiPilot.UI.Fluent;

/// <summary>
/// Stacks cards into one group: full width, a hairline gap between them, and the rounding of the
/// first and last set so the run reads as a single panel.
/// </summary>
internal sealed class CardStack : Panel
{
    public CardStack()
    {
        Dock = DockStyle.Top;
        AutoSize = false;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, Theme.GroupGap);
    }

    /// <summary>Adds a card to the bottom of the group.</summary>
    public T Add<T>(T card) where T : Control
    {
        Controls.Add(card);
        Relayout();
        return card;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Relayout();
    }

    private bool _laying;

    private void Relayout()
    {
        if (_laying)
        {
            return;
        }

        _laying = true;
        try
        {
            var cards = Controls.OfType<Control>().Where(c => c.Visible).ToList();
            int gap = FluentPaint.Dpi(this, Theme.CardGap);
            int y = 0;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                card.Left = 0;
                card.Top = y;
                card.Width = Width;

                if (card is SettingsCard settings)
                {
                    settings.Place = cards.Count == 1 ? SettingsCard.Position.Only
                        : i == 0 ? SettingsCard.Position.Top
                        : i == cards.Count - 1 ? SettingsCard.Position.Bottom
                        : SettingsCard.Position.Middle;
                }

                y += card.Height + gap;
            }

            // The trailing gap belongs between groups, not inside one.
            Height = Math.Max(0, y - gap);
        }
        finally
        {
            _laying = false;
        }
    }
}

/// <summary>A plain rounded card for content that is not a settings row — an editor, a list.</summary>
internal sealed class CardPanel : Panel, IThemedControl
{
    public CardPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Padding = new Padding(16);
    }

    public void OnThemeChanged()
    {
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) =>
        FluentPaint.DrawSurface(e.Graphics, ClientRectangle, FluentPaint.Dpi(this, Theme.CardRadius), Theme.Card, Theme.CardStroke);
}

/// <summary>The small heading that sits above a group of cards.</summary>
internal sealed class GroupHeader : Label, IThemedControl
{
    public GroupHeader(string text)
    {
        Text = text;

        // Full width rather than shrink-to-fit: an auto-sized label docks to the leading edge and
        // stays there, which leaves the heading stranded on the wrong side in Arabic.
        AutoSize = false;
        Dock = DockStyle.Top;
        Height = 30;
        Font = Typography.BodyStrong;
        ForeColor = Theme.Text;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(2, 0, 2, 6);
        TextAlign = ContentAlignment.BottomLeft;
        RightToLeft = Theme.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
    }

    public void OnThemeChanged() => ForeColor = Theme.Text;
}
