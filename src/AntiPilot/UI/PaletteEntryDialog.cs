namespace AntiPilot.UI;

/// <summary>Builds or edits one palette entry: a name and the action behind it.</summary>
public sealed class PaletteEntryDialog : Form
{
    private readonly TextBox _labelBox = new();

    // A palette entry that opens the palette would be a loop, so that mode is not on offer here.
    private readonly ActionEditor _editor = new() { AllowPalette = false };

    /// <summary>The entry as edited. Only meaningful once the dialog returns OK.</summary>
    public KeyAction Entry { get; private set; }

    public PaletteEntryDialog(KeyAction? existing)
    {
        Entry = existing?.Clone() ?? new KeyAction();

        Text = Strings.PaletteAdd;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 500);
        Theme.ScaleFromDesignDpi(this);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Icon = AppIcon.Load(32);

        _labelBox.Dock = DockStyle.Fill;
        _labelBox.Text = Entry.Label ?? string.Empty;
        _labelBox.PlaceholderText = Entry.Describe();

        _editor.NothingHint = Strings.NothingHintTap;
        _editor.Action = Entry;

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 14, 16, 6),
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.Controls.Add(new Label { Text = Strings.PaletteLabelCaption, AutoSize = true, Margin = new Padding(0, 0, 0, 3) }, 0, 0);
        top.Controls.Add(_labelBox, 0, 1);

        var frame = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
        frame.Controls.Add(_editor);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 8) };
        body.Controls.Add(frame);

        Controls.Add(body);
        Controls.Add(top);
        Controls.Add(BuildButtons());

        Theme.Watch(this);
    }

    private Control BuildButtons()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 6, 16, 12),
        };

        var ok = new Button
        {
            Text = Strings.Ok,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(18, 5, 18, 5),
        };
        ok.Click += (_, _) => Accept();

        var cancel = new Button
        {
            Text = Strings.Cancel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 5, 14, 5),
            DialogResult = DialogResult.Cancel,
        };

        row.Controls.Add(ok);
        row.Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
        return row;
    }

    private void Accept()
    {
        var entry = _editor.Action;

        if (!entry.IsConfigured)
        {
            MessageBox.Show(this, Strings.NothingConfiguredYet, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // An empty name is not an error: Describe() already produces something sensible, and
        // storing the placeholder as if the user had typed it would be a lie.
        entry.Label = string.IsNullOrWhiteSpace(_labelBox.Text) ? null : _labelBox.Text.Trim();

        Entry = entry;
        DialogResult = DialogResult.OK;
        Close();
    }
}
