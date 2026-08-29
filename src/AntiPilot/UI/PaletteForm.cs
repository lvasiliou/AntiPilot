using AntiPilot.UI.Fluent;

namespace AntiPilot.UI;

/// <summary>
/// The quick-launch palette: one key press, a short list, and the thing you wanted.
///
/// Keyboard-first on purpose. The user has just pressed a key, so their hands are already in the
/// right place — 1-9 runs an entry outright, typing filters, Enter takes the top match and Esc
/// leaves without doing anything. Clicking works too, but nothing here requires the mouse.
/// </summary>
public sealed class PaletteForm : Form
{
    private const int MaxNumbered = 9;

    /// <summary>The height of one row, at the design DPI.</summary>
    private const int RowHeight = 28;

    /// <summary>The filter box plus the padding above and below the list, at the design DPI.</summary>
    private const int ChromeHeight = 58;

    private readonly List<KeyAction> _entries;
    private readonly List<KeyAction> _shown = [];
    private readonly TextBox _filter = new();
    private readonly ListBox _list = new();

    private KeyAction? _chosen;

    private PaletteForm(List<KeyAction> entries)
    {
        _entries = entries;

        Text = Strings.PaletteWindowTitle;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = true;
        KeyPreview = true;
        Theme.ScaleFromDesignDpi(this);
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Padding = new Padding(1);
        ClientSize = new Size(460, 0);
        Icon = AppIcon.Load(32);

        _filter.Dock = DockStyle.Top;
        _filter.BorderStyle = BorderStyle.None;
        _filter.BackColor = Theme.Card;
        _filter.ForeColor = Theme.Text;
        _filter.Font = new Font(Font.FontFamily, 13f);
        _filter.PlaceholderText = Strings.PaletteFilterPlaceholder;
        _filter.Margin = Padding.Empty;
        _filter.TextChanged += (_, _) => ApplyFilter();

        _list.Dock = DockStyle.Fill;
        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = Theme.ListBackground;
        _list.ForeColor = Theme.Text;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.IntegralHeight = false;
        _list.DrawItem += OnDrawItem;
        _list.Click += (_, _) => RunSelected();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                RunSelected();
                e.Handled = true;
            }
        };

        // A one-pixel band of window colour reads as a border against any wallpaper.
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.ListBackground, Padding = new Padding(10, 8, 10, 8) };
        body.Controls.Add(_list);

        var head = new Panel { Dock = DockStyle.Top, BackColor = Theme.Card, Padding = new Padding(12, 10, 12, 10), Height = 42 };
        head.Controls.Add(_filter);

        Controls.Add(body);
        Controls.Add(head);

        ApplyFilter();

        Theme.ApplyDirection(this);
        Deactivate += (_, _) => Close();
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Opens the palette for the entries in <paramref name="config"/>. False means it could not be
    /// shown — which, when nothing has been added to the palette yet, is worth saying out loud.
    /// </summary>
    public static bool Show(AppConfig config, ActionFeedback feedback)
    {
        // A palette entry that opens the palette would recurse; drop those rather than police it later.
        var entries = config.Palette
            .Where(action => action.IsConfigured && action.Kind != ActionKind.Palette)
            .ToList();

        if (entries.Count == 0)
        {
            Log.Write("Palette requested but no entries are configured.");
            Notifier.ShowError(Strings.PaletteShort, Strings.PaletteEmptyWarning, feedback);
            return false;
        }

        // A key press has not touched WinForms until now, and this has to happen before the first
        // control exists or the palette comes up unthemed and at the wrong DPI.
        WinFormsHost.Ensure();

        var form = new PaletteForm(entries);

        if (Application.MessageLoop)
        {
            // Inside the tray process: hand it to the loop that is already running.
            form.Show();
            form.Activate();
            return true;
        }

        Application.Run(form);
        return true;
    }

    private void ApplyFilter()
    {
        var needle = _filter.Text.Trim();

        _shown.Clear();
        _shown.AddRange(string.IsNullOrEmpty(needle)
            ? _entries
            : _entries.Where(entry => entry.Describe().Contains(needle, StringComparison.CurrentCultureIgnoreCase)));

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var entry in _shown)
        {
            _list.Items.Add(entry.Describe());
        }

        _list.EndUpdate();

        if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }

        ResizeToContent();
    }

    private void ResizeToContent()
    {
        int rows = Math.Clamp(_list.Items.Count, 1, 12);

        // Every number in this sum is a design-DPI measurement and none of them is scaled for us:
        // ListBox.ItemHeight is not bounds, and a size written into ClientSize after the window has
        // already been scaled has missed its turn. Recomputing the row height from the constant
        // rather than multiplying whatever is there keeps this safe to call as often as it likes.
        _list.ItemHeight = FluentPaint.Dpi(this, RowHeight);
        ClientSize = new Size(
            ClientSize.Width,
            FluentPaint.Dpi(this, ChromeHeight) + rows * _list.ItemHeight);
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _shown.Count)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;
        var background = selected ? Theme.Accent : Theme.ListBackground;
        var foreground = selected ? Theme.AccentText : Theme.Text;

        using (var brush = new SolidBrush(background))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        var text = _shown[e.Index].Describe();
        var textBounds = e.Bounds with { X = e.Bounds.X + 34, Width = e.Bounds.Width - 38 };

        if (e.Index < MaxNumbered)
        {
            var numberBounds = e.Bounds with { X = e.Bounds.X + 8, Width = 22 };
            using var numberBrush = new SolidBrush(selected ? Theme.AccentText : Theme.SecondaryText);
            e.Graphics.DrawString((e.Index + 1).ToString(), e.Font ?? Font, numberBrush, numberBounds,
                new StringFormat { LineAlignment = StringAlignment.Center });
        }

        using var brushText = new SolidBrush(foreground);
        e.Graphics.DrawString(text, e.Font ?? Font, brushText, textBounds,
            new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Escape:
                Close();
                e.Handled = true;
                return;

            case Keys.Enter:
                RunSelected();
                e.Handled = true;
                return;

            case Keys.Down when _list.Items.Count > 0:
                _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _list.Items.Count - 1);
                e.Handled = true;
                return;

            case Keys.Up when _list.Items.Count > 0:
                _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0);
                e.Handled = true;
                return;
        }

        // 1-9 run an entry outright. Only without modifiers: Alt+4 and friends belong to the entry
        // the user is about to launch, not to us.
        if (e.Modifiers == Keys.None && e.KeyCode is >= Keys.D1 and <= Keys.D9)
        {
            Run(e.KeyCode - Keys.D1);
            e.Handled = true;
        }
    }

    private void RunSelected() => Run(_list.SelectedIndex);

    private void Run(int index)
    {
        if (index < 0 || index >= _shown.Count)
        {
            return;
        }

        _chosen = _shown[index];
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        if (_chosen is null)
        {
            return;
        }

        // Run after the window has gone: a Menu-key or shortcut entry has to land on whatever the
        // user was using before, not on the palette.
        var action = _chosen;
        _chosen = null;
        Log.Write($"Palette entry chosen: {action.Describe()}");
        ActionRunner.Run(action, ActionFeedback.Balloon);
    }
}
