using System.ComponentModel;
using AntiPilot.Interop;

namespace AntiPilot.UI;

/// <summary>Editor for a single <see cref="KeyAction"/>. Used once per tab (tap / press-and-hold).</summary>
public sealed class ActionEditor : UserControl
{
    private readonly ComboBox _modeCombo = new();
    private readonly Panel _content = new();

    private readonly TableLayoutPanel _appPanel;
    private readonly TableLayoutPanel _filePanel;
    private readonly Panel _menuPanel;
    private readonly Panel _nothingPanel;

    private readonly PictureBox _appIcon = new();
    private readonly Label _appName = new();
    private readonly Label _appAumid = new();

    private readonly TextBox _pathBox = new();
    private readonly TextBox _argsBox = new();
    private readonly TextBox _workDirBox = new();

    private readonly Label _nothingLabel = new();

    private KeyAction _action = new();
    private Bitmap? _iconBitmap;
    private bool _loading;

    public event EventHandler? ActionChanged;

    /// <summary>Text shown when the selected mode is "Nothing".</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string NothingHint
    {
        get => _nothingLabel.Text;
        set => _nothingLabel.Text = value;
    }

    public ActionEditor()
    {
        AutoScaleMode = AutoScaleMode.Font;
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;

        _modeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeCombo.Dock = DockStyle.Fill;
        _modeCombo.Margin = new Padding(0, 2, 0, 12);
        _modeCombo.Items.AddRange(new object[]
        {
            "Nothing",
            "Launch an installed app  (Start menu or Microsoft Store)",
            "Launch a program, file, folder or link",
            "Act as the Menu key  (the old context-menu key)",
        });
        _modeCombo.SelectedIndexChanged += (_, _) =>
        {
            _action.Kind = SelectedKind;
            UpdatePanels();
            OnActionChanged();
        };

        _appPanel = BuildAppPanel();
        _filePanel = BuildFilePanel();
        _menuPanel = BuildMenuPanel();
        _nothingPanel = BuildNothingPanel();

        _content.Dock = DockStyle.Fill;
        _content.Margin = Padding.Empty;
        foreach (Control panel in new Control[] { _appPanel, _filePanel, _menuPanel, _nothingPanel })
        {
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            _content.Controls.Add(panel);
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16, 14, 16, 12),
            Margin = Padding.Empty,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label { Text = "Do this:", AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
        root.Controls.Add(_modeCombo, 0, 1);
        root.Controls.Add(_content, 0, 2);

        Controls.Add(root);
    }

    private ActionKind SelectedKind => _modeCombo.SelectedIndex switch
    {
        1 => ActionKind.ShellApp,
        2 => ActionKind.File,
        3 => ActionKind.MenuKey,
        _ => ActionKind.None,
    };

    private static int IndexForKind(ActionKind kind) => kind switch
    {
        ActionKind.ShellApp => 1,
        ActionKind.File => 2,
        ActionKind.MenuKey => 3,
        _ => 0,
    };

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public KeyAction Action
    {
        get => _action;
        set
        {
            _loading = true;
            _action = value.Clone();
            _modeCombo.SelectedIndex = IndexForKind(_action.Kind);
            _appName.Text = string.IsNullOrWhiteSpace(_action.DisplayName) ? "(no app chosen)" : _action.DisplayName!;
            _appAumid.Text = _action.Aumid ?? string.Empty;
            _pathBox.Text = _action.Path ?? string.Empty;
            _argsBox.Text = _action.Arguments ?? string.Empty;
            _workDirBox.Text = _action.WorkingDirectory ?? string.Empty;
            LoadIconAsync(_action.Aumid);
            UpdatePanels();
            _loading = false;
        }
    }

    private void OnActionChanged()
    {
        if (!_loading)
        {
            ActionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdatePanels()
    {
        _appPanel.Visible = _action.Kind == ActionKind.ShellApp;
        _filePanel.Visible = _action.Kind == ActionKind.File;
        _menuPanel.Visible = _action.Kind == ActionKind.MenuKey;
        _nothingPanel.Visible = _action.Kind == ActionKind.None;
    }

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.SecondaryText,
        Margin = new Padding(0, 10, 0, 0),
    };

    // ---- app panel ---------------------------------------------------------

    private TableLayoutPanel BuildAppPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 4,
            Margin = Padding.Empty,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _appIcon.Size = new Size(48, 48);
        _appIcon.SizeMode = PictureBoxSizeMode.Zoom;
        _appIcon.Margin = new Padding(0, 0, 12, 0);

        _appName.AutoSize = true;
        _appName.Font = new Font(Font, FontStyle.Bold);
        _appName.Text = "(no app chosen)";
        _appName.Margin = new Padding(0, 4, 0, 0);

        _appAumid.AutoSize = true;
        _appAumid.ForeColor = Theme.SecondaryText;
        _appAumid.Margin = new Padding(0, 0, 0, 4);

        var choose = new Button
        {
            Text = "Choose app…",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 5, 12, 5),
            Margin = new Padding(0, 12, 0, 0),
        };
        choose.Click += (_, _) =>
        {
            using var dialog = new AppPickerDialog(_action.Aumid);
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedApp is { } app)
            {
                _action.Aumid = app.ParsingName;
                _action.DisplayName = app.Name;
                _appName.Text = app.Name;
                _appAumid.Text = app.ParsingName;
                LoadIconAsync(app.ParsingName);
                OnActionChanged();
            }
        };

        panel.Controls.Add(_appIcon, 0, 0);
        panel.SetRowSpan(_appIcon, 2);
        panel.Controls.Add(_appName, 1, 0);
        panel.Controls.Add(_appAumid, 1, 1);
        panel.Controls.Add(choose, 0, 2);
        panel.SetColumnSpan(choose, 2);

        var hint = Hint("Anything in the Start menu's app list works here, including Microsoft Store apps.");
        panel.Controls.Add(hint, 0, 3);
        panel.SetColumnSpan(hint, 2);

        return panel;
    }

    private void LoadIconAsync(string? parsingName)
    {
        _appIcon.Image = null;
        _iconBitmap?.Dispose();
        _iconBitmap = null;

        if (string.IsNullOrWhiteSpace(parsingName))
        {
            return;
        }

        var pixels = (int)(48 * DeviceDpi / 96.0);
        var thread = new Thread(() =>
        {
            var bitmap = ShellApps.TryGetIcon(parsingName!, pixels);
            if (bitmap is null)
            {
                return;
            }

            try
            {
                BeginInvoke(() =>
                {
                    _iconBitmap?.Dispose();
                    _iconBitmap = bitmap;
                    _appIcon.Image = bitmap;
                });
            }
            catch (Exception)
            {
                bitmap.Dispose(); // The control went away first.
            }
        })
        { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    // ---- file panel --------------------------------------------------------

    private TableLayoutPanel BuildFilePanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 8,
            Margin = Padding.Empty,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 7; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _pathBox.Dock = DockStyle.Fill;
        _pathBox.PlaceholderText = @"C:\Windows\System32\notepad.exe   or   https://example.com";
        _pathBox.TextChanged += (_, _) =>
        {
            _action.Path = _pathBox.Text;
            OnActionChanged();
        };

        var browse = new Button
        {
            Text = "Browse…",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(8, 3, 0, 3),
        };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Pick a program or file",
                Filter = "Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url|All files|*.*",
                CheckFileExists = true,
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _pathBox.Text = dialog.FileName;
            }
        };

        _argsBox.Dock = DockStyle.Fill;
        _argsBox.TextChanged += (_, _) =>
        {
            _action.Arguments = _argsBox.Text;
            OnActionChanged();
        };

        _workDirBox.Dock = DockStyle.Fill;
        _workDirBox.TextChanged += (_, _) =>
        {
            _action.WorkingDirectory = _workDirBox.Text;
            OnActionChanged();
        };

        AddCaption(panel, "Program, file, folder or link", 0);
        panel.Controls.Add(_pathBox, 0, 1);
        panel.Controls.Add(browse, 1, 1);

        AddCaption(panel, "Arguments (optional)", 2);
        panel.Controls.Add(_argsBox, 0, 3);
        panel.SetColumnSpan(_argsBox, 2);

        AddCaption(panel, "Start in (optional)", 4);
        panel.Controls.Add(_workDirBox, 0, 5);
        panel.SetColumnSpan(_workDirBox, 2);

        var hint = Hint("Environment variables such as %USERPROFILE% are expanded.");
        panel.Controls.Add(hint, 0, 6);
        panel.SetColumnSpan(hint, 2);

        return panel;

        static void AddCaption(TableLayoutPanel target, string text, int row)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, row == 0 ? 0 : 12, 0, 3),
            };

            target.Controls.Add(label, 0, row);
            target.SetColumnSpan(label, 2);
        }
    }

    // ---- static panels -----------------------------------------------------

    private Panel BuildMenuPanel()
    {
        var panel = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = "The key sends the Menu key (VK_APPS), so the context menu of whatever is focused " +
                   "opens — exactly like a right-click.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0),
        }, 0, 0);

        panel.Controls.Add(Hint(
            "Windows blocks synthetic input aimed at windows running as administrator, so this does " +
            "nothing while an elevated app is in the foreground."), 0, 1);

        return panel;
    }

    private Panel BuildNothingPanel()
    {
        var panel = new TableLayoutPanel { ColumnCount = 1, RowCount = 1, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _nothingLabel.Text = "Nothing happens.";
        _nothingLabel.AutoSize = true;
        _nothingLabel.ForeColor = Theme.SecondaryText;
        _nothingLabel.Margin = Padding.Empty;

        panel.Controls.Add(_nothingLabel, 0, 0);
        return panel;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iconBitmap?.Dispose();
        }

        base.Dispose(disposing);
    }
}
