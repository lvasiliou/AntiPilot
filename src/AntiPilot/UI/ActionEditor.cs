using System.ComponentModel;
using AntiPilot.Interop;
using AntiPilot.UI.Fluent;

namespace AntiPilot.UI;

/// <summary>Editor for a single <see cref="KeyAction"/>. Used once per tab and again in the dialogs.</summary>
public sealed class ActionEditor : UserControl
{
    /// <summary>
    /// Chords worth offering outright. Ctrl+Alt+Delete is deliberately absent: it is the secure
    /// attention sequence, which no synthesised input can trigger, so an entry for it would be a
    /// button that silently does nothing.
    /// </summary>
    private static readonly (string Label, string Chord)[] Presets =
    [
        (nameof(Strings.PresetTaskManager), "Ctrl+Shift+Escape"),
        (nameof(Strings.PresetClipboard), "Win+V"),
        (nameof(Strings.PresetSnip), "Win+Shift+S"),
        (nameof(Strings.PresetExplorer), "Win+E"),
        (nameof(Strings.PresetEmoji), "Win+."),
        (nameof(Strings.PresetPrintScreen), "PrintScreen"),
        (nameof(Strings.PresetLock), "Win+L"),
        (nameof(Strings.PresetShowDesktop), "Win+D"),
        (nameof(Strings.PresetPlayPause), "MediaPlayPause"),
        (nameof(Strings.PresetMute), "VolumeMute"),
    ];

    private readonly ComboBox _modeCombo = new();
    private readonly Panel _content = new();

    private readonly TableLayoutPanel _appPanel;
    private readonly TableLayoutPanel _filePanel;
    private readonly Panel _menuPanel;
    private readonly TableLayoutPanel _hotkeyPanel;
    private readonly Panel _palettePanel;
    private readonly Panel _nothingPanel;

    private readonly PictureBox _appIcon = new();
    private readonly Label _appName = new();
    private readonly Label _appAumid = new();
    private readonly ComboBox _appBehaviour;

    private readonly TextBox _pathBox = new();
    private readonly TextBox _argsBox = new();
    private readonly TextBox _workDirBox = new();
    private readonly ComboBox _fileBehaviour;

    /// <summary>Stops the two behaviour combos echoing each other's changes back and forth.</summary>
    private bool _syncingBehaviour;

    private readonly HotkeyBox _hotkeyBox = new();
    private readonly ComboBox _presetCombo = new();

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

    /// <summary>
    /// Hides the palette option. A palette entry that opens the palette would be a loop, so the
    /// editor used to build those entries does not offer it.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowPalette { get; init; } = true;

    public ActionEditor()
    {
        AutoScaleMode = AutoScaleMode.Font;
        Dock = DockStyle.Fill;

        // The editor always sits inside a card, so it takes the card colour rather than the page one.
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = Typography.Body;

        _modeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeCombo.Dock = DockStyle.Fill;
        _modeCombo.Margin = new Padding(0, 2, 0, 12);
        _modeCombo.SelectedIndexChanged += (_, _) =>
        {
            _action.Kind = SelectedKind;
            UpdatePanels();
            OnActionChanged();
        };

        // Two panels, each with its own copy of the same question, so both are built up front and
        // kept in step by SyncBehaviourCombos.
        _appBehaviour = BuildBehaviourCombo();
        _fileBehaviour = BuildBehaviourCombo();

        _appPanel = BuildAppPanel();
        _filePanel = BuildFilePanel();
        _menuPanel = BuildMenuPanel();
        _hotkeyPanel = BuildHotkeyPanel();
        _palettePanel = BuildPalettePanel();
        _nothingPanel = BuildNothingPanel();

        _content.Dock = DockStyle.Fill;
        _content.Margin = Padding.Empty;
        foreach (Control panel in new Control[] { _appPanel, _filePanel, _menuPanel, _hotkeyPanel, _palettePanel, _nothingPanel })
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
            Padding = Padding.Empty,
            Margin = Padding.Empty,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label { Text = Strings.DoThis, AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
        root.Controls.Add(_modeCombo, 0, 1);
        root.Controls.Add(_content, 0, 2);

        Controls.Add(root);
        PopulateModes();
    }

    // ---- mode list ---------------------------------------------------------

    /// <summary>The kinds offered, in the order they appear. Index arithmetic would break as soon as
    /// the palette entry is hidden, so the mapping is kept explicitly.</summary>
    private readonly List<ActionKind> _modes = [];

    private void PopulateModes()
    {
        _modes.Clear();
        _modeCombo.Items.Clear();

        Add(ActionKind.None, Strings.ModeNothing);
        Add(ActionKind.ShellApp, Strings.ModeShellApp);
        Add(ActionKind.File, Strings.ModeFile);
        Add(ActionKind.MenuKey, Strings.ModeMenuKey);
        Add(ActionKind.Hotkey, Strings.ModeHotkey);

        if (AllowPalette)
        {
            Add(ActionKind.Palette, Strings.ModePalette);
        }

        _modeCombo.SelectedIndex = 0;

        void Add(ActionKind kind, string label)
        {
            _modes.Add(kind);
            _modeCombo.Items.Add(label);
        }
    }

    private ActionKind SelectedKind =>
        _modeCombo.SelectedIndex >= 0 && _modeCombo.SelectedIndex < _modes.Count
            ? _modes[_modeCombo.SelectedIndex]
            : ActionKind.None;

    private int IndexForKind(ActionKind kind)
    {
        int index = _modes.IndexOf(kind);
        return index < 0 ? 0 : index;
    }

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
            _appName.Text = string.IsNullOrWhiteSpace(_action.DisplayName) ? Strings.NoAppChosen : _action.DisplayName!;
            _appAumid.Text = _action.Aumid ?? string.Empty;
            _pathBox.Text = _action.Path ?? string.Empty;
            _argsBox.Text = _action.Arguments ?? string.Empty;
            _workDirBox.Text = _action.WorkingDirectory ?? string.Empty;
            SyncBehaviourCombos();
            _hotkeyBox.Value = HotkeyDefinition.TryParse(_action.Hotkey, out var parsed) ? parsed : null;
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
        _hotkeyPanel.Visible = _action.Kind == ActionKind.Hotkey;
        _palettePanel.Visible = _action.Kind == ActionKind.Palette;
        _nothingPanel.Visible = _action.Kind == ActionKind.None;
    }

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.SecondaryText,
        Tag = Theme.SecondaryTag,
        Margin = new Padding(0, 10, 0, 0),
        MaximumSize = new Size(560, 0),
    };

    private ComboBox BuildBehaviourCombo()
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 0),
        };

        combo.Items.AddRange([Strings.BehaviourAlways, Strings.BehaviourFocus, Strings.BehaviourToggle]);
        combo.SelectedIndex = 0;
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingBehaviour)
            {
                return;
            }

            _action.Behaviour = (LaunchBehaviour)Math.Max(0, combo.SelectedIndex);
            SyncBehaviourCombos();
            OnActionChanged();
        };

        return combo;
    }

    private void SyncBehaviourCombos()
    {
        _syncingBehaviour = true;
        _appBehaviour.SelectedIndex = (int)_action.Behaviour;
        _fileBehaviour.SelectedIndex = (int)_action.Behaviour;
        _syncingBehaviour = false;
    }

    // ---- app panel ---------------------------------------------------------

    private TableLayoutPanel BuildAppPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 6,
            Margin = Padding.Empty,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _appIcon.Size = new Size(48, 48);
        _appIcon.SizeMode = PictureBoxSizeMode.Zoom;
        _appIcon.Margin = new Padding(0, 0, 12, 0);

        _appName.AutoSize = true;
        _appName.Font = new Font(Font, FontStyle.Bold);
        _appName.Text = Strings.NoAppChosen;
        _appName.Margin = new Padding(0, 4, 0, 0);

        _appAumid.AutoSize = true;
        _appAumid.ForeColor = Theme.SecondaryText;
        _appAumid.Tag = Theme.SecondaryTag;
        _appAumid.Margin = new Padding(0, 0, 0, 4);

        var choose = new FluentButton
        {
            Text = Strings.ChooseApp,
            Height = 32,
            Margin = new Padding(0, 12, 0, 0),
        };
        choose.Width = choose.PreferredWidth;
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

        var behaviourCaption = new Label
        {
            Text = Strings.WhenAlreadyRunning,
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 3),
        };

        panel.Controls.Add(_appIcon, 0, 0);
        panel.SetRowSpan(_appIcon, 2);
        panel.Controls.Add(_appName, 1, 0);
        panel.Controls.Add(_appAumid, 1, 1);
        panel.Controls.Add(choose, 0, 2);
        panel.SetColumnSpan(choose, 2);

        panel.Controls.Add(behaviourCaption, 0, 3);
        panel.SetColumnSpan(behaviourCaption, 2);
        panel.Controls.Add(_appBehaviour, 0, 4);
        panel.SetColumnSpan(_appBehaviour, 2);

        var hint = Hint(Strings.AppHint + " " + Strings.BehaviourHint);
        panel.Controls.Add(hint, 0, 5);
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
            RowCount = 10,
            Margin = Padding.Empty,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 9; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _pathBox.Dock = DockStyle.Fill;
        _pathBox.PlaceholderText = Strings.FilePlaceholder;
        _pathBox.TextChanged += (_, _) =>
        {
            _action.Path = _pathBox.Text;
            OnActionChanged();
        };

        var browse = new FluentButton
        {
            Text = Strings.Browse,
            Height = 30,
            Margin = new Padding(8, 2, 0, 2),
        };
        browse.Width = browse.PreferredWidth;
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = Strings.FileDialogTitle,
                Filter = Strings.FileDialogFilter,
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

        AddCaption(panel, Strings.FileCaptionPath, 0);
        panel.Controls.Add(_pathBox, 0, 1);
        panel.Controls.Add(browse, 1, 1);

        AddCaption(panel, Strings.FileCaptionArgs, 2);
        panel.Controls.Add(_argsBox, 0, 3);
        panel.SetColumnSpan(_argsBox, 2);

        AddCaption(panel, Strings.FileCaptionWorkDir, 4);
        panel.Controls.Add(_workDirBox, 0, 5);
        panel.SetColumnSpan(_workDirBox, 2);

        AddCaption(panel, Strings.WhenAlreadyRunning, 6);
        panel.Controls.Add(_fileBehaviour, 0, 7);
        panel.SetColumnSpan(_fileBehaviour, 2);

        var hint = Hint(Strings.FileHint);
        panel.Controls.Add(hint, 0, 8);
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

    // ---- hotkey panel ------------------------------------------------------

    private TableLayoutPanel BuildHotkeyPanel()
    {
        var panel = new TableLayoutPanel { ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _hotkeyBox.Dock = DockStyle.Fill;
        _hotkeyBox.Margin = new Padding(0, 2, 0, 0);
        _hotkeyBox.HotkeyChanged += (_, _) =>
        {
            _action.Hotkey = _hotkeyBox.Value?.Format();
            OnActionChanged();
        };

        _presetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _presetCombo.Dock = DockStyle.Fill;
        _presetCombo.Margin = new Padding(0, 2, 0, 0);
        _presetCombo.Items.Add(string.Empty);
        foreach (var (label, chord) in Presets)
        {
            _presetCombo.Items.Add($"{Strings.Get(label)}  ({chord})");
        }

        _presetCombo.SelectedIndex = 0;
        _presetCombo.SelectedIndexChanged += (_, _) =>
        {
            int index = _presetCombo.SelectedIndex - 1;
            if (index < 0 || index >= Presets.Length)
            {
                return;
            }

            if (HotkeyDefinition.TryParse(Presets[index].Chord, out var parsed))
            {
                _hotkeyBox.Value = parsed;
            }
        };

        panel.Controls.Add(new Label { Text = Strings.HotkeyCaption, AutoSize = true, Margin = new Padding(0, 0, 0, 3) }, 0, 0);
        panel.Controls.Add(_hotkeyBox, 0, 1);
        panel.Controls.Add(new Label { Text = Strings.HotkeyPresets, AutoSize = true, Margin = new Padding(0, 12, 0, 3) }, 0, 2);
        panel.Controls.Add(_presetCombo, 0, 3);
        panel.Controls.Add(Hint(Strings.HotkeyHint + " " + Strings.ElevatedHint), 0, 4);

        return panel;
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
            Text = Strings.MenuKeyBody,
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 0, 0, 0),
        }, 0, 0);

        panel.Controls.Add(Hint(Strings.ElevatedHint), 0, 1);

        return panel;
    }

    private Panel BuildPalettePanel()
    {
        var panel = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = Strings.PaletteBody,
            AutoSize = true,
            MaximumSize = new Size(560, 0),
        }, 0, 0);

        panel.Controls.Add(Hint(Strings.PaletteEmptyWarning), 0, 1);
        return panel;
    }

    private Panel BuildNothingPanel()
    {
        var panel = new TableLayoutPanel { ColumnCount = 1, RowCount = 1, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _nothingLabel.Text = Strings.Nothing;
        _nothingLabel.AutoSize = true;
        _nothingLabel.ForeColor = Theme.SecondaryText;
        _nothingLabel.Tag = Theme.SecondaryTag;
        _nothingLabel.MaximumSize = new Size(560, 0);
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
