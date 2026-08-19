using AntiPilot.Interop;

namespace AntiPilot.UI;

/// <summary>Builds or edits one "while this app is in front" rule.</summary>
public sealed class AppRuleDialog : Form
{
    private readonly TextBox _processBox = new();
    private readonly ActionEditor _editor = new();

    /// <summary>The rule as edited. Only meaningful once the dialog returns OK.</summary>
    public AppRule Rule { get; private set; }

    public AppRuleDialog(AppRule? existing)
    {
        Rule = existing?.Clone() ?? new AppRule();

        Text = Strings.PerAppAdd;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 520);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Icon = AppIcon.Load(32);

        _processBox.Dock = DockStyle.Fill;
        _processBox.Text = Rule.ProcessName ?? string.Empty;
        _processBox.PlaceholderText = "chrome";

        var pick = new Button
        {
            Text = Strings.PerAppPickRunning,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(8, 3, 0, 3),
        };
        pick.Click += (_, _) => PickRunningApp();

        _editor.NothingHint = Strings.NothingHintTap;
        _editor.Action = Rule.Action;

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(16, 14, 16, 6),
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 3; i++)
        {
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var caption = new Label { Text = Strings.PerAppProcess, AutoSize = true, Margin = new Padding(0, 0, 0, 3) };
        top.Controls.Add(caption, 0, 0);
        top.SetColumnSpan(caption, 2);
        top.Controls.Add(_processBox, 0, 1);
        top.Controls.Add(pick, 1, 1);

        var hint = new Label
        {
            Text = Strings.PerAppProcessHint,
            AutoSize = true,
            ForeColor = Theme.SecondaryText,
            Tag = Theme.SecondaryTag,
            Margin = new Padding(0, 6, 0, 0),
            MaximumSize = new Size(520, 0),
        };
        top.Controls.Add(hint, 0, 2);
        top.SetColumnSpan(hint, 2);

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
        if (string.IsNullOrWhiteSpace(_processBox.Text))
        {
            MessageBox.Show(this, Strings.PerAppProcessHint, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            _processBox.Focus();
            return;
        }

        Rule = new AppRule
        {
            ProcessName = AppRule.Normalise(_processBox.Text),
            Action = _editor.Action,
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private void PickRunningApp()
    {
        var menu = new ContextMenuStrip();

        foreach (var (processName, title) in WindowFinder.ListWindowedApps())
        {
            var label = string.IsNullOrWhiteSpace(title) ? processName : $"{processName}  —  {title}";
            var name = processName;
            menu.Items.Add(new ToolStripMenuItem(label, null, (_, _) => _processBox.Text = name));
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem(Strings.PerAppNoRules) { Enabled = false });
        }

        menu.Show(_processBox, new Point(0, _processBox.Height));
    }
}
