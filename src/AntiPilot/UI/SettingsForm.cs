using System.Diagnostics;
using System.Globalization;
using AntiPilot.UI.Fluent;

namespace AntiPilot.UI;

/// <summary>
/// The settings window, laid out the way Windows 11 lays out settings: a navigation rail down the
/// left, a scrolling page of cards on the right, and the commit buttons along the bottom.
///
/// Everything visible here is drawn by the controls in the Fluent folder rather than by WinForms,
/// which is the only way to get the corner radii, the accent colour and the type ramp right.
/// </summary>
public sealed class SettingsForm : Form
{
    /// <summary>
    /// The languages with a translation. Tags only; the names come from Windows so each one is
    /// written the way its own speakers write it.
    /// </summary>
    private static readonly string[] Languages =
        ["en", "ru", "es", "zh-Hans", "pt-BR", "tr", "ja", "ko", "ar", "id", "zh-Hant", "el"];

    private AppConfig _config = AppConfig.Load();

    private readonly ActionEditor _tapEditor = new();
    private readonly ActionEditor _doubleEditor = new();
    private readonly ToggleSwitch _doubleEnabled = new();
    private readonly FluentSlider _doubleWindow = new();
    private readonly SettingsCard _doubleWindowCard = new();
    private readonly Label _doubleWarning = new();
    private readonly CardPanel _doubleEditorCard = new();

    private readonly ListView _rulesList = new();
    private readonly ListView _paletteList = new();

    private readonly SettingsCard _statusCard = new();
    private readonly FluentButton _saveButton = new();
    private readonly ToggleSwitch _startupToggle = new();
    private readonly SettingsCard _startupCard = new();
    private readonly ComboBox _languageCombo = new();

    private readonly NavigationRail _rail = new();
    private readonly Panel _pageHost = new();
    private readonly List<Control> _pages = [];

    private bool _dirty;
    private bool _updatingStartupToggle;
    private bool _loadingLanguage;

    public SettingsForm()
    {
        Text = Strings.SettingsTitle;

        Font = Typography.Body;
        ClientSize = new Size(900, 700);
        MinimumSize = new Size(780, 520);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Icon = AppIcon.Load(32);
        DoubleBuffered = true;

        _tapEditor.NothingHint = Strings.NothingHintTap;
        _tapEditor.ActionChanged += (_, _) => MarkDirty();
        _tapEditor.Action = _config.Tap;

        _doubleEditor.NothingHint = Strings.NothingHintDouble;
        _doubleEditor.ActionChanged += (_, _) => MarkDirty();
        _doubleEditor.Action = _config.DoubleTap;

        BuildPages();

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(Theme.PagePadding, 4, Theme.PagePadding, 0),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _rail.Dock = DockStyle.Fill;
        _rail.Margin = new Padding(0, 0, 20, 0);
        _rail.SelectedIndexChanged += (_, _) => ShowPage(_rail.SelectedIndex);

        _pageHost.Dock = DockStyle.Fill;
        _pageHost.BackColor = Color.Transparent;
        // Each page scrolls itself; a scrolling host as well would nest two scrollbars.
        _pageHost.AutoScroll = false;
        _pageHost.Padding = new Padding(0, 0, 4, 0);

        body.Controls.Add(_rail, 0, 0);
        body.Controls.Add(_pageHost, 1, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(BuildHeader(), 0, 0);
        layout.Controls.Add(BuildStatusCard(), 0, 1);
        layout.Controls.Add(body, 0, 2);
        layout.Controls.Add(BuildFooter(), 0, 3);

        Controls.Add(layout);

        ShowPage(0);

        FormClosing += OnFormClosing;

        // The status card invites a trip to Windows Settings, so it has to be right when the user
        // comes back — reading it once at construction leaves it stale exactly when it matters.
        Activated += (_, _) => RefreshStatus();

        Theme.Watch(this);
        HandleCreated += (_, _) => Theme.ApplyWindowEffects(this, mica: false);
    }

    // ---- chrome ------------------------------------------------------------

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(Theme.PagePadding, 18, Theme.PagePadding, 12),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        header.Controls.Add(new Label
        {
            Text = Strings.AppName,
            Font = Typography.Title,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        }, 0, 0);

        header.Controls.Add(new Label
        {
            Text = Strings.SettingsTagline,
            Font = Typography.Body,
            ForeColor = Theme.SecondaryText,
            Tag = Theme.SecondaryTag,
            BackColor = Color.Transparent,
            AutoSize = true,
            Margin = new Padding(1, 0, 0, 0),
        }, 0, 1);

        return header;
    }

    private Control BuildStatusCard()
    {
        var open = new FluentButton { Text = Strings.OpenWindowsSettings, Height = 32 };
        open.Width = open.PreferredWidth;
        open.Click += (_, _) => CopilotKeyStatus.OpenWindowsSettings();

        _statusCard.Glyph = Typography.Glyphs.Keyboard;
        _statusCard.Title = CopilotKeyStatus.Describe();
        _statusCard.Action = open;
        _statusCard.Height = 60;
        _statusCard.Dock = DockStyle.Top;
        _statusCard.Margin = Padding.Empty;

        var host = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Padding = new Padding(Theme.PagePadding, 0, Theme.PagePadding, 8),
            Height = 68,
        };
        host.Controls.Add(_statusCard);
        return host;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(Theme.PagePadding, 12, Theme.PagePadding, 16),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var test = new FluentButton { Text = Strings.TestIt, Glyph = Typography.Glyphs.Play, Height = 34 };
        test.Width = test.PreferredWidth;
        test.Click += (_, _) => TestCurrentAction();

        var cancel = new FluentButton { Text = Strings.Cancel, Height = 34, Margin = new Padding(3, 3, 8, 3) };
        cancel.Width = Math.Max(cancel.PreferredWidth, 96);
        cancel.Click += (_, _) =>
        {
            // An explicit Cancel means "throw my edits away", so skip the save prompt.
            _dirty = false;
            Close();
        };

        _saveButton.Text = Strings.Save;
        _saveButton.IsAccent = true;
        _saveButton.Height = 34;
        _saveButton.Width = Math.Max(_saveButton.PreferredWidth, 110);
        _saveButton.Click += (_, _) =>
        {
            // Always live: a greyed-out Save just reads as a broken button. With nothing to
            // save it simply closes.
            if (!_dirty || Save())
            {
                Close();
            }
        };

        footer.Controls.Add(test, 0, 0);
        footer.Controls.Add(new Panel { Margin = Padding.Empty, Size = Size.Empty, BackColor = Color.Transparent }, 1, 0);
        footer.Controls.Add(cancel, 2, 0);
        footer.Controls.Add(_saveButton, 3, 0);

        AcceptButton = null;
        CancelButton = null;
        return footer;
    }

    // ---- pages -------------------------------------------------------------

    private void BuildPages()
    {
        AddPage(Typography.Glyphs.Keyboard, Strings.TabSinglePress, BuildSinglePage());
        AddPage(Typography.Glyphs.Stopwatch, Strings.TabDoublePress, BuildDoublePage());
        AddPage(Typography.Glyphs.AppIcon, Strings.TabPerApp, BuildRulesPage());
        AddPage(Typography.Glyphs.AllApps, Strings.TabPalette, BuildPalettePage());
        AddPage(Typography.Glyphs.Settings, Strings.TabGeneral, BuildGeneralPage());
    }

    private void AddPage(string glyph, string label, Control page)
    {
        _rail.Add(glyph, label);
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pages.Add(page);
        _pageHost.Controls.Add(page);
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < _pages.Count; i++)
        {
            _pages[i].Visible = i == index;
        }

        _pageHost.AutoScrollPosition = Point.Empty;
    }

    /// <summary>
    /// A scrolling page.
    ///
    /// AutoScroll on its own does nothing for the content here, because a Dock.Fill child simply
    /// shrinks to whatever room is left and clips what will not fit — there is never anything
    /// outside the viewport for the scrollbar to reach. AutoScrollMinSize is what fixes it: it
    /// gives the page a canvas at least <paramref name="minHeight"/> tall, docked children lay out
    /// against that instead of against the window, and the scrollbar appears as soon as the window
    /// is shorter. Without it, turning on the double press pushed its editor off the bottom of a
    /// default-sized window with no way to reach it.
    /// </summary>
    private static Panel NewPage(int minHeight)
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoScroll = true,
            AutoScrollMinSize = new Size(0, minHeight),
        };
    }

    private Control BuildSinglePage()
    {
        // Tall enough for the file editor, which has the most rows of any mode.
        var page = NewPage(400);

        var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(18) };
        card.Controls.Add(_tapEditor);

        page.Controls.Add(card);
        return page;
    }

    private Control BuildDoublePage()
    {
        // Two cards, the cost note, and a full editor below them.
        var page = NewPage(620);

        _doubleEnabled.SetCheckedQuietly(_config.DoubleTapEnabled);
        _doubleEnabled.CheckedChanged += (_, _) =>
        {
            _config.DoubleTapEnabled = _doubleEnabled.Checked;
            RefreshDoubleState();
            MarkDirty();
        };

        var enableCard = new SettingsCard
        {
            Glyph = Typography.Glyphs.Stopwatch,
            Title = Strings.DoubleTapEnable,
            Description = Strings.DoubleTapEnableDescription,
            Action = _doubleEnabled,
            Height = 64,
        };

        _doubleWindow.Minimum = AppConfig.MinDoubleTapWindowMs;
        _doubleWindow.Maximum = AppConfig.MaxDoubleTapWindowMs;
        _doubleWindow.Step = 50;
        _doubleWindow.Width = 240;
        _doubleWindow.Height = 32;
        _doubleWindow.SetValueQuietly(_config.DoubleTapWindowMs);
        _doubleWindow.ValueChanged += (_, _) =>
        {
            _config.DoubleTapWindowMs = _doubleWindow.Value;
            RefreshDoubleState();
            MarkDirty();
        };

        _doubleWindowCard.Glyph = Typography.Glyphs.Stopwatch;
        _doubleWindowCard.Title = Strings.DoubleTapWindow;
        _doubleWindowCard.Action = _doubleWindow;
        _doubleWindowCard.Height = 60;

        var group = new CardStack { Height = 130 };
        group.Add(enableCard);
        group.Add(_doubleWindowCard);

        _doubleWarning.AutoSize = false;
        _doubleWarning.Dock = DockStyle.Top;
        _doubleWarning.Height = 54;
        _doubleWarning.Font = Typography.Caption;
        _doubleWarning.ForeColor = Theme.SecondaryText;
        _doubleWarning.Tag = Theme.SecondaryTag;
        _doubleWarning.BackColor = Color.Transparent;
        _doubleWarning.Padding = new Padding(2, 8, 2, 12);

        _doubleEditorCard.Dock = DockStyle.Fill;
        _doubleEditorCard.Padding = new Padding(18);
        _doubleEditorCard.Controls.Add(_doubleEditor);

        // Docked children fill in reverse order of addition, so the editor goes in first.
        page.Controls.Add(_doubleEditorCard);
        page.Controls.Add(_doubleWarning);
        page.Controls.Add(group);

        RefreshDoubleState();
        return page;
    }

    private void RefreshDoubleState()
    {
        bool on = _doubleEnabled.Checked;

        _doubleWindow.Enabled = on;
        _doubleWindowCard.Description = Strings.Format(Strings.DoubleTapMilliseconds, _config.DoubleTapWindowMs);
        _doubleEditor.Enabled = on;
        _doubleEditorCard.Enabled = on;
        _doubleWarning.Text = on
            ? Strings.Format(Strings.DoubleTapCostWarning, _config.DoubleTapWindowMs)
            : Strings.DoubleTapDisabledHint;
    }

    private Control BuildRulesPage()
    {
        var page = NewPage(340);

        _rulesList.View = View.Details;
        _rulesList.FullRowSelect = true;
        _rulesList.MultiSelect = false;
        _rulesList.HideSelection = false;
        _rulesList.Dock = DockStyle.Fill;
        _rulesList.BorderStyle = BorderStyle.None;
        _rulesList.BackColor = Theme.Card;
        _rulesList.ForeColor = Theme.Text;
        _rulesList.Font = Typography.Body;
        _rulesList.Columns.Add(Strings.PerAppColumnApp, 220);
        _rulesList.Columns.Add(Strings.PerAppColumnAction, 420);
        _rulesList.DoubleClick += (_, _) => EditRule();

        page.Controls.Add(BuildListPage(
            Strings.PerAppIntro,
            _rulesList,
            [
                (Strings.PerAppAdd, AddRule),
                (Strings.Edit, EditRule),
                (Strings.Remove, RemoveRule),
            ]));

        RefreshRules();
        return page;
    }

    private Control BuildPalettePage()
    {
        var page = NewPage(340);

        _paletteList.View = View.Details;
        _paletteList.FullRowSelect = true;
        _paletteList.MultiSelect = false;
        _paletteList.HideSelection = false;
        _paletteList.Dock = DockStyle.Fill;
        _paletteList.BorderStyle = BorderStyle.None;
        _paletteList.BackColor = Theme.Card;
        _paletteList.ForeColor = Theme.Text;
        _paletteList.Font = Typography.Body;
        _paletteList.Columns.Add("#", 40);
        _paletteList.Columns.Add(Strings.PaletteColumnLabel, 220);
        _paletteList.Columns.Add(Strings.PaletteColumnAction, 380);
        _paletteList.DoubleClick += (_, _) => EditPaletteEntry();

        page.Controls.Add(BuildListPage(
            Strings.PaletteIntro,
            _paletteList,
            [
                (Strings.PaletteAdd, AddPaletteEntry),
                (Strings.Edit, EditPaletteEntry),
                (Strings.Remove, RemovePaletteEntry),
                (Strings.PaletteMoveUp, () => MovePaletteEntry(-1)),
                (Strings.PaletteMoveDown, () => MovePaletteEntry(+1)),
            ]));

        return page;
    }

    private static Control BuildListPage(string intro, Control list, (string Label, Action OnClick)[] buttons)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = intro,
            AutoSize = false,
            Height = 44,
            Dock = DockStyle.Top,
            Font = Typography.Caption,
            ForeColor = Theme.SecondaryText,
            Tag = Theme.SecondaryTag,
            BackColor = Color.Transparent,
            Margin = new Padding(2, 0, 2, 10),
        }, 0, 0);

        var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        card.Controls.Add(list);
        layout.Controls.Add(card, 0, 1);

        if (list is ListView view)
        {
            // Fixed column widths leave a horizontal scrollbar on a window this wide, which reads as
            // a mistake. The last column simply takes whatever is left.
            void FitColumns()
            {
                if (view.Columns.Count == 0 || view.ClientSize.Width <= 0)
                {
                    return;
                }

                int used = 0;
                for (int i = 0; i < view.Columns.Count - 1; i++)
                {
                    used += view.Columns[i].Width;
                }

                view.Columns[^1].Width = Math.Max(80, view.ClientSize.Width - used - 4);
            }

            view.Resize += (_, _) => FitColumns();
            view.HandleCreated += (_, _) => FitColumns();
        }

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 12, 0, 0),
        };

        foreach (var (label, onClick) in buttons)
        {
            var button = new FluentButton
            {
                Text = label,
                Height = 32,
                Margin = new Padding(0, 0, 8, 0),
            };

            button.Width = button.PreferredWidth;
            button.Click += (_, _) => onClick();
            row.Controls.Add(button);
        }

        layout.Controls.Add(row, 0, 2);
        return layout;
    }

    private Control BuildGeneralPage()
    {
        // Three groups with their headings.
        var page = NewPage(560);

        // Groups are docked top in reverse, so the last one added ends up first on screen.
        var about = new CardStack { Height = 128 };
        about.Add(LinkCard(Typography.Glyphs.Page, Strings.OpenLog, Strings.OpenLogDescription, () =>
        {
            Log.Write("Log opened from settings.");
            Process.Start(new ProcessStartInfo { FileName = Log.LogPath, UseShellExecute = true })?.Dispose();
        }));
        about.Add(LinkCard(Typography.Glyphs.Info, Strings.About, Strings.AboutTagline, () =>
        {
            using var dialog = new AboutDialog();
            dialog.ShowDialog(this);
        }));

        var transfer = new CardStack { Height = 128 };
        transfer.Add(LinkCard(Typography.Glyphs.Export, Strings.ExportButton, Strings.ExportTitle, Export));
        transfer.Add(LinkCard(Typography.Glyphs.Import, Strings.ImportButton, Strings.ImportTitle, Import));

        _startupToggle.CheckedChanged += (_, _) => OnStartupToggleChanged();
        _startupCard.Glyph = Typography.Glyphs.Ringer;
        _startupCard.Title = Strings.ShowTrayAndStart;
        _startupCard.Action = _startupToggle;
        _startupCard.Height = 64;

        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.FlatStyle = FlatStyle.System;
        _languageCombo.Font = Typography.Body;
        _languageCombo.Width = 190;
        PopulateLanguages();
        _languageCombo.SelectedIndexChanged += (_, _) => OnLanguageChanged();

        var languageCard = new SettingsCard
        {
            Glyph = Typography.Glyphs.Language,
            Title = Strings.Language,
            Description = Strings.LanguageRestartHint,
            Action = _languageCombo,
            Height = 64,
        };

        var behaviour = new CardStack { Height = 132 };
        behaviour.Add(_startupCard);
        behaviour.Add(languageCard);

        page.Controls.Add(WithHeader(Strings.GeneralGroupAbout, about));
        page.Controls.Add(WithHeader(Strings.GeneralGroupTransfer, transfer));
        page.Controls.Add(WithHeader(Strings.GeneralGroupBehaviour, behaviour));

        RefreshStartupToggle();
        return page;

        static Control WithHeader(string title, Control group)
        {
            var host = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                Height = group.Height + 30 + Theme.GroupGap,
                Padding = new Padding(0, 0, 0, Theme.GroupGap),
            };

            host.Controls.Add(group);
            host.Controls.Add(new GroupHeader(title));
            return host;
        }
    }

    /// <summary>A card that behaves like a link: the whole row is clickable, with a chevron on the right.</summary>
    private static SettingsCard LinkCard(string glyph, string title, string description, Action onClick)
    {
        var chevron = new Label
        {
            Text = Typography.Glyphs.ChevronRight,
            Font = Typography.SmallIcon,
            ForeColor = Theme.SecondaryText,
            Tag = Theme.SecondaryTag,
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(20, 20),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var card = new SettingsCard
        {
            Glyph = glyph,
            Title = title,
            Description = description,
            Action = chevron,
            Height = 60,
            Interactive = true,
            Cursor = Cursors.Hand,
        };

        card.Click += (_, _) => onClick();
        chevron.Click += (_, _) => onClick();
        return card;
    }

    // ---- per-app rules -----------------------------------------------------

    private void RefreshRules()
    {
        _rulesList.BeginUpdate();
        _rulesList.Items.Clear();

        foreach (var rule in _config.AppRules)
        {
            _rulesList.Items.Add(new ListViewItem([rule.ProcessName ?? string.Empty, rule.Action.Describe()]));
        }

        _rulesList.EndUpdate();
    }

    private void AddRule()
    {
        using var dialog = new AppRuleDialog(null);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (_config.AppRules.Any(rule => rule.Matches(dialog.Rule.ProcessName)))
        {
            MessageBox.Show(this, Strings.PerAppDuplicate, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _config.AppRules.Add(dialog.Rule);
        RefreshRules();
        MarkDirty();
    }

    private void EditRule()
    {
        int index = SelectedIndex(_rulesList);
        if (index < 0)
        {
            return;
        }

        using var dialog = new AppRuleDialog(_config.AppRules[index]);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.AppRules[index] = dialog.Rule;
        RefreshRules();
        MarkDirty();
    }

    private void RemoveRule()
    {
        int index = SelectedIndex(_rulesList);
        if (index < 0)
        {
            return;
        }

        _config.AppRules.RemoveAt(index);
        RefreshRules();
        MarkDirty();
    }

    // ---- palette -----------------------------------------------------------

    private void RefreshPalette()
    {
        _paletteList.BeginUpdate();
        _paletteList.Items.Clear();

        for (int i = 0; i < _config.Palette.Count; i++)
        {
            var entry = _config.Palette[i];

            // Only the first nine get a number, because only those can be run by pressing one.
            var number = i < 9 ? (i + 1).ToString() : string.Empty;
            _paletteList.Items.Add(new ListViewItem([number, entry.Describe(), DescribeKind(entry)]));
        }

        _paletteList.EndUpdate();
    }

    private static string DescribeKind(KeyAction entry) => entry.Kind switch
    {
        ActionKind.ShellApp => entry.DisplayName ?? entry.Aumid ?? string.Empty,
        ActionKind.File => entry.Path ?? string.Empty,
        ActionKind.Hotkey => entry.Hotkey ?? string.Empty,
        ActionKind.MenuKey => Strings.MenuKeyShort,
        _ => string.Empty,
    };

    private void AddPaletteEntry()
    {
        using var dialog = new PaletteEntryDialog(null);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.Palette.Add(dialog.Entry);
        RefreshPalette();
        MarkDirty();
    }

    private void EditPaletteEntry()
    {
        int index = SelectedIndex(_paletteList);
        if (index < 0)
        {
            return;
        }

        using var dialog = new PaletteEntryDialog(_config.Palette[index]);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.Palette[index] = dialog.Entry;
        RefreshPalette();
        MarkDirty();
    }

    private void RemovePaletteEntry()
    {
        int index = SelectedIndex(_paletteList);
        if (index < 0)
        {
            return;
        }

        _config.Palette.RemoveAt(index);
        RefreshPalette();
        MarkDirty();
    }

    private void MovePaletteEntry(int delta)
    {
        int index = SelectedIndex(_paletteList);
        int target = index + delta;

        if (index < 0 || target < 0 || target >= _config.Palette.Count)
        {
            return;
        }

        (_config.Palette[index], _config.Palette[target]) = (_config.Palette[target], _config.Palette[index]);
        RefreshPalette();

        _paletteList.Items[target].Selected = true;
        _paletteList.Items[target].Focused = true;
        MarkDirty();
    }

    private static int SelectedIndex(ListView list) =>
        list.SelectedIndices.Count == 0 ? -1 : list.SelectedIndices[0];

    // ---- general -----------------------------------------------------------

    private void PopulateLanguages()
    {
        _loadingLanguage = true;

        _languageCombo.Items.Add(Strings.LanguageSystem);
        foreach (var tag in Languages)
        {
            string name;
            try
            {
                name = CultureInfo.GetCultureInfo(tag).NativeName;
            }
            catch (CultureNotFoundException)
            {
                name = tag;
            }

            _languageCombo.Items.Add(name);
        }

        int index = _config.Language is null ? -1 : Array.FindIndex(Languages,
            tag => string.Equals(tag, _config.Language, StringComparison.OrdinalIgnoreCase));

        _languageCombo.SelectedIndex = index < 0 ? 0 : index + 1;
        _loadingLanguage = false;
    }

    private void OnLanguageChanged()
    {
        if (_loadingLanguage)
        {
            return;
        }

        _config.Language = _languageCombo.SelectedIndex <= 0 ? null : Languages[_languageCombo.SelectedIndex - 1];
        MarkDirty();

        // Relabelling every control that is already on screen is more machinery than the change is
        // worth, and the window is about to be saved and closed anyway.
        MessageBox.Show(this, Strings.LanguageRestartHint, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RefreshStartupToggle()
    {
        var state = TrayStartup.GetState();

        _updatingStartupToggle = true;
        _startupToggle.SetCheckedQuietly(state == TrayStartup.Availability.On);
        _updatingStartupToggle = false;

        switch (state)
        {
            case TrayStartup.Availability.Unavailable:
                _startupToggle.Enabled = false;
                _startupCard.Description = Strings.TrayNeedsInstall;
                break;

            case TrayStartup.Availability.BlockedByUser:
                _startupToggle.Enabled = true;
                _startupCard.Description = Strings.TrayBlocked;
                break;

            default:
                _startupToggle.Enabled = true;
                _startupCard.Description = Strings.TrayDescription;
                break;
        }
    }

    private void OnStartupToggleChanged()
    {
        if (_updatingStartupToggle)
        {
            return;
        }

        if (_startupToggle.Checked)
        {
            var result = TrayStartup.Enable();

            if (result == TrayStartup.Availability.BlockedByUser)
            {
                MessageBox.Show(this, Strings.TrayStartupBlockedBody, Strings.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStartupToggle();
                return;
            }

            if (result != TrayStartup.Availability.On)
            {
                MessageBox.Show(this, Strings.TrayStartupFailed, Strings.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshStartupToggle();
                return;
            }

            // Flipping the switch should produce an icon now, not only after the next sign-in.
            StartTrayProcess();
        }
        else
        {
            // And switching it off should take the icon away now, not only at the next sign-in.
            TrayStartup.Disable();
            TrayApplication.RequestExit();
            RefreshStartupToggle();
        }
    }

    /// <summary>Launches the tray entry point in a second process. Does nothing if it is up.</summary>
    private void StartTrayProcess()
    {
        if (TrayApplication.IsRunning())
        {
            return;
        }

        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(exe, "--tray") { UseShellExecute = false })?.Dispose();
            Log.Write("Tray icon started from the settings window.");
        }
        catch (Exception ex)
        {
            Log.Write($"Could not start the tray icon: {ex}");
            MessageBox.Show(this, $"{Strings.TrayCouldNotStart}\r\n\r\n{ex.Message}", Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ---- import and export -------------------------------------------------

    private void Export()
    {
        using var dialog = new SaveFileDialog
        {
            Title = Strings.ExportTitle,
            Filter = Strings.ConfigFilter,
            FileName = "antipilot-settings.json",
            DefaultExt = "json",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            Collect().SaveTo(dialog.FileName);
            Log.Write($"Settings exported to '{dialog.FileName}'.");
            MessageBox.Show(this, Strings.ExportDone, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{Strings.CouldNotSave}\r\n\r\n{ex.Message}", Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Import()
    {
        using var dialog = new OpenFileDialog
        {
            Title = Strings.ImportTitle,
            Filter = Strings.ConfigFilter,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var imported = AppConfig.LoadFrom(dialog.FileName);
        if (imported is null)
        {
            MessageBox.Show(this, Strings.ImportFailed, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(this, Strings.ImportConfirm, Strings.AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        // The tray introduction is about this machine, not about the settings, so it does not
        // travel: importing on a new PC should still explain where the icon went.
        imported.TrayIntroShown = _config.TrayIntroShown;

        _config = imported;
        _tapEditor.Action = _config.Tap;
        _doubleEditor.Action = _config.DoubleTap;
        _doubleEnabled.SetCheckedQuietly(_config.DoubleTapEnabled);
        _doubleWindow.SetValueQuietly(_config.DoubleTapWindowMs);
        RefreshDoubleState();
        RefreshRules();
        RefreshPalette();
        MarkDirty();

        Log.Write($"Settings imported from '{dialog.FileName}'.");
        MessageBox.Show(this, Strings.ImportDone, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---- saving ------------------------------------------------------------

    private void RefreshStatus() => _statusCard.Title = CopilotKeyStatus.Describe();

    private void MarkDirty() => _dirty = true;

    /// <summary>The config as the window currently stands, editors included.</summary>
    private AppConfig Collect()
    {
        _config.Tap = _tapEditor.Action;
        _config.DoubleTap = _doubleEditor.Action;
        _config.DoubleTapEnabled = _doubleEnabled.Checked;
        return _config;
    }

    private bool Save()
    {
        var config = Collect();

        if (!ConfirmValidation(config))
        {
            return false;
        }

        try
        {
            config.Save();
            _dirty = false;
            RefreshStatus();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{Strings.CouldNotSave}\r\n\r\n{ex.Message}", Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    /// <summary>
    /// Warns about actions that point at something no longer there. Advisory, not a veto: a target
    /// can be a removable drive or an app about to be reinstalled, and refusing the save outright
    /// would be wrong in both cases.
    /// </summary>
    private bool ConfirmValidation(AppConfig config)
    {
        var problems = new List<string>();

        Check(Strings.TabSinglePress, config.Tap);

        if (config.DoubleTapEnabled)
        {
            Check(Strings.TabDoublePress, config.DoubleTap);
        }

        foreach (var rule in config.AppRules)
        {
            Check(rule.ProcessName ?? Strings.TabPerApp, rule.Action);
        }

        foreach (var entry in config.Palette)
        {
            Check(entry.Describe(), entry);
        }

        if (problems.Count == 0)
        {
            return true;
        }

        var message = string.Join(Environment.NewLine, problems);
        return MessageBox.Show(this,
            $"{message}{Environment.NewLine}{Environment.NewLine}{Strings.ValidationContinue}?",
            Strings.ValidationTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;

        void Check(string where, KeyAction action)
        {
            if (ActionValidator.Validate(action) is { } problem)
            {
                problems.Add($"{where}: {problem}");
            }
        }
    }

    private void TestCurrentAction()
    {
        var action = _tapEditor.Action;
        if (!action.IsConfigured)
        {
            MessageBox.Show(this, Strings.NothingConfiguredYet, Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        switch (action.Kind)
        {
            case ActionKind.MenuKey:
                MessageBox.Show(this, Strings.MenuKeyTestHint, Strings.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;

            case ActionKind.Hotkey:
                MessageBox.Show(this, Strings.HotkeyTestHint, Strings.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
        }

        ActionRunner.Run(action, ActionFeedback.Dialog, Collect());
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_dirty)
        {
            return;
        }

        var answer = MessageBox.Show(this, Strings.SaveBeforeClosing, Strings.AppName,
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        switch (answer)
        {
            case DialogResult.Yes:
                e.Cancel = !Save();
                break;
            case DialogResult.Cancel:
                e.Cancel = true;
                break;
        }
    }
}
