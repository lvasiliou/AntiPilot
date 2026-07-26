using System.Diagnostics;

namespace AntiPilot.UI;

public sealed class SettingsForm : Form
{
    private readonly AppConfig _config = AppConfig.Load();

    private readonly ActionEditor _editor = new();
    private readonly Label _statusLabel = new();
    private readonly Button _saveButton = new();
    private readonly CheckBox _startupCheck = new();

    private bool _dirty;
    private bool _updatingStartupCheck;

    public SettingsForm()
    {
        Text = "AntiPilot";

        // Fixed size: everything below is laid out for exactly this width, and there is nothing
        // here that benefits from being resized.
        ClientSize = new Size(640, 620);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        SizeGripStyle = SizeGripStyle.Hide;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Icon = AppIcon.Load(32);

        _editor.NothingHint = "The key does nothing at all. Pick something above.";
        _editor.ActionChanged += (_, _) => MarkDirty();
        _editor.Action = _config.Tap;

        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty,
        };
        frame.Controls.Add(_editor);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 8), Margin = Padding.Empty };
        body.Controls.Add(frame);

        // A table keeps the vertical order explicit instead of relying on docking z-order.
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(BuildHeader(), 0, 0);
        layout.Controls.Add(BuildStatusStrip(), 0, 1);
        layout.Controls.Add(body, 0, 2);
        layout.Controls.Add(BuildStartupOption(), 0, 3);
        layout.Controls.Add(BuildFooter(), 0, 4);

        Controls.Add(layout);

        FormClosing += OnFormClosing;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(16, 14, 16, 10),
            BackColor = Theme.Window,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        header.Controls.Add(new Label
        {
            Text = "AntiPilot",
            Font = new Font(Font.FontFamily, 16f, FontStyle.Regular),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        }, 0, 0);

        header.Controls.Add(new Label
        {
            Text = "Decide what the Copilot key (and Win+C) actually does.",
            ForeColor = Theme.SecondaryText,
            AutoSize = true,
            Margin = new Padding(2, 0, 0, 0),
        }, 0, 1);

        return header;
    }

    private Control BuildStatusStrip()
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Card,
            Margin = new Padding(16, 0, 16, 10),
            Padding = new Padding(12, 8, 12, 8),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // One line only: a label's preferred height never accounts for wrapping, so anything
        // longer would be clipped by the auto-sized row. Keep the status strings short.
        _statusLabel.AutoSize = true;
        _statusLabel.Anchor = AnchorStyles.Left;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Margin = new Padding(0, 0, 12, 0);

        var openSettings = new Button
        {
            Text = "Open Windows settings",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Padding = new Padding(8, 4, 8, 4),
        };
        openSettings.Click += (_, _) => CopilotKeyStatus.OpenWindowsSettings();

        card.Controls.Add(_statusLabel, 0, 0);
        card.Controls.Add(openSettings, 1, 0);

        RefreshStatus();
        return card;
    }

    private Control BuildStartupOption()
    {
        // Straight into the grid cell: an AutoSize panel around it collapses to nothing.
        _startupCheck.Text = "Show the tray icon, and start it when I sign in";
        _startupCheck.AutoSize = true;
        _startupCheck.Anchor = AnchorStyles.Left;
        _startupCheck.Margin = new Padding(17, 8, 16, 4);
        _startupCheck.CheckedChanged += (_, _) => OnStartupCheckChanged();

        RefreshStartupCheck();
        return _startupCheck;
    }

    private void RefreshStartupCheck()
    {
        var state = TrayStartup.GetState();

        _updatingStartupCheck = true;
        _startupCheck.Checked = state == TrayStartup.Availability.On;
        _updatingStartupCheck = false;

        switch (state)
        {
            case TrayStartup.Availability.Unavailable:
                _startupCheck.Enabled = false;
                _startupCheck.Text = "Tray icon at sign-in (needs the installed version)";
                break;

            case TrayStartup.Availability.BlockedByUser:
                _startupCheck.Enabled = true;
                _startupCheck.Text = "Tray icon at sign-in (switched off in Task Manager)";
                break;

            default:
                _startupCheck.Enabled = true;
                _startupCheck.Text = "Show the tray icon, and start it when I sign in";
                break;
        }
    }

    private void OnStartupCheckChanged()
    {
        if (_updatingStartupCheck)
        {
            return;
        }

        if (_startupCheck.Checked)
        {
            var result = TrayStartup.Enable();

            if (result == TrayStartup.Availability.BlockedByUser)
            {
                MessageBox.Show(this,
                    "Windows remembers that this was switched off in Task Manager, and only you can " +
                    "switch it back on: Task Manager → Startup apps → \"AntiPilot tray icon\".",
                    "AntiPilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStartupCheck();
                return;
            }

            if (result != TrayStartup.Availability.On)
            {
                MessageBox.Show(this, "Windows would not enable the startup task.", "AntiPilot",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshStartupCheck();
                return;
            }

            // Ticking the box should produce an icon now, not only after the next sign-in.
            StartTrayProcess();
        }
        else
        {
            // Unticking should take the icon away now, not only at the next sign-in.
            TrayStartup.Disable();
            TrayApplication.RequestExit();
            RefreshStartupCheck();
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
            MessageBox.Show(this, $"Could not start the tray icon.\r\n\r\n{ex.Message}", "AntiPilot",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private Control BuildFooter()
    {
        // Two rows: links above, buttons below. Squeezing both onto one row made them overlap as
        // soon as the link text grew.
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(16, 4, 16, 12),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var test = new Button
        {
            Text = "Test it",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 5, 12, 5),
        };
        test.Click += (_, _) => TestCurrentAction();

        var links = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 8),
            WrapContents = false,
        };

        var log = new LinkLabel { Text = "Open log", AutoSize = true, Margin = new Padding(0, 4, 16, 0) };
        log.Click += (_, _) =>
        {
            Log.Write("Log opened from settings.");
            Process.Start(new ProcessStartInfo { FileName = Log.LogPath, UseShellExecute = true })?.Dispose();
        };

        var about = new LinkLabel { Text = "About", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        about.Click += (_, _) =>
        {
            using var dialog = new AboutDialog();
            dialog.ShowDialog(this);
        };

        links.Controls.Add(log);
        links.Controls.Add(about);

        var cancel = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 5, 14, 5),
            Margin = new Padding(3, 3, 8, 3),
        };
        cancel.Click += (_, _) =>
        {
            // An explicit Cancel means "throw my edits away", so skip the save prompt.
            _dirty = false;
            Close();
        };

        _saveButton.Text = "Save";
        _saveButton.AutoSize = true;
        _saveButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _saveButton.Padding = new Padding(18, 5, 18, 5);
        _saveButton.Click += (_, _) =>
        {
            // Always live: a greyed-out Save just reads as a broken button. With nothing to
            // save it simply closes.
            if (!_dirty || Save())
            {
                Close();
            }
        };

        footer.Controls.Add(links, 0, 0);
        footer.SetColumnSpan(links, 4);

        footer.Controls.Add(test, 0, 1);
        footer.Controls.Add(new Panel { Margin = Padding.Empty, Size = new Size(0, 0) }, 1, 1);
        footer.Controls.Add(cancel, 2, 1);
        footer.Controls.Add(_saveButton, 3, 1);

        AcceptButton = _saveButton;
        CancelButton = cancel;
        return footer;
    }

    private void RefreshStatus() => _statusLabel.Text = CopilotKeyStatus.Describe();

    private void MarkDirty() => _dirty = true;

    private bool Save()
    {
        _config.Tap = _editor.Action;

        try
        {
            _config.Save();
            _dirty = false;
            RefreshStatus();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save settings.\r\n\r\n{ex.Message}", "AntiPilot",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private void TestCurrentAction()
    {
        var action = _editor.Action;
        if (!action.IsConfigured)
        {
            MessageBox.Show(this, "Nothing is configured yet.", "AntiPilot",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (action.Kind == ActionKind.MenuKey)
        {
            MessageBox.Show(this,
                "The Menu key action can only be tested from the keyboard: close this window, focus " +
                "something else and press the key.",
                "AntiPilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ActionRunner.Run(action);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_dirty)
        {
            return;
        }

        var answer = MessageBox.Show(this, "Save your changes before closing?", "AntiPilot",
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
