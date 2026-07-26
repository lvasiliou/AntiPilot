namespace AntiPilot.UI;

/// <summary>
/// The notification-area presence: a way back to the settings window now that pressing the
/// key launches the configured app instead of opening anything.
/// </summary>
public sealed class TrayApplication : ApplicationContext
{
    /// <summary>Named so a second tray process can detect the first one and bow out.</summary>
    private const string MutexName = @"Local\AntiPilot.TrayIcon";

    /// <summary>Signalled to ask a tray icon in another process to go away.</summary>
    private const string ExitEventName = @"Local\AntiPilot.TrayExit";

    private static Mutex? _singleInstance;

    /// <summary>The tray icon of this process, when this process is the one hosting it.</summary>
    private static TrayApplication? _current;

    /// <summary>Marshals the cross-process exit request onto the UI thread.</summary>
    private readonly Control _marshaller = new();
    private EventWaitHandle? _exitSignal;
    private RegisteredWaitHandle? _exitWait;
    private bool _exitWhenSettingsClose;

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _actionItem;
    private readonly ToolStripMenuItem _startupItem;
    private SettingsForm? _settings;

    public TrayApplication()
    {
        _actionItem = new ToolStripMenuItem("Run the key action", null, (_, _) => RunConfiguredAction());
        _startupItem = new ToolStripMenuItem("Start when I sign in", null, (_, _) => ToggleStartup())
        {
            CheckOnClick = false,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings()) { Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add(_actionItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Customise the Copilot key in Windows…", null,
            (_, _) => CopilotKeyStatus.OpenWindowsSettings()));
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripMenuItem("About AntiPilot…", null, (_, _) => ShowAbout()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));
        menu.Opening += (_, _) => RefreshLabels();

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Load(32),
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => OpenSettings();

        _marshaller.CreateControl();
        _current = this;
        ListenForExitRequest();

        RefreshLabels();
        Log.Write("Tray icon started.");
        IntroduceOnce();
    }

    /// <summary>
    /// Turns the icon off, wherever it lives: this process if we own it, otherwise the one that does.
    /// </summary>
    public static void RequestExit()
    {
        if (_current is not null)
        {
            _current.HideAndExit();
            return;
        }

        try
        {
            if (EventWaitHandle.TryOpenExisting(ExitEventName, out var handle))
            {
                using (handle)
                {
                    handle.Set();
                    Log.Write("Asked the tray icon process to exit.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Could not signal the tray icon to exit: {ex.Message}");
        }
    }

    private void ListenForExitRequest()
    {
        try
        {
            _exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
            _exitWait = ThreadPool.RegisterWaitForSingleObject(
                _exitSignal,
                (_, _) =>
                {
                    try
                    {
                        _marshaller.BeginInvoke(HideAndExit);
                    }
                    catch (Exception)
                    {
                        // The window is already gone; nothing to do.
                    }
                },
                null,
                Timeout.Infinite,
                executeOnlyOnce: true);
        }
        catch (Exception ex)
        {
            Log.Write($"Could not set up the tray exit signal: {ex.Message}");
        }
    }

    private void HideAndExit()
    {
        _icon.Visible = false;

        // The settings window may be a child of this process; let it finish first.
        if (_settings is { IsDisposed: false })
        {
            _exitWhenSettingsClose = true;
            return;
        }

        Log.Write("Tray icon closed.");
        ExitThread();
    }

    /// <summary>True when another process already owns the tray icon.</summary>
    public static bool IsRunningElsewhere()
    {
        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out bool created);
        if (created)
        {
            return false;
        }

        _singleInstance.Dispose();
        _singleInstance = null;
        return true;
    }

    /// <summary>Probe that does not claim ownership, for use from the settings window.</summary>
    public static bool IsRunning()
    {
        if (Mutex.TryOpenExisting(MutexName, out var existing))
        {
            existing.Dispose();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Windows 11 drops brand new notification icons into the hidden overflow, so the first run
    /// looks like nothing happened. Say where it went, once.
    /// </summary>
    private void IntroduceOnce()
    {
        var config = AppConfig.Load();
        if (config.TrayIntroShown)
        {
            return;
        }

        try
        {
            _icon.BalloonTipTitle = "AntiPilot is in the notification area";
            _icon.BalloonTipText =
                "Windows hides new icons: click the ^ next to the clock and drag AntiPilot onto the " +
                "taskbar to keep it visible.";
            _icon.BalloonTipIcon = ToolTipIcon.Info;
            _icon.ShowBalloonTip(10_000);

            config.TrayIntroShown = true;
            config.Save();
        }
        catch (Exception ex)
        {
            Log.Write($"Could not show the tray introduction: {ex.Message}");
        }
    }

    private void RefreshLabels()
    {
        var config = AppConfig.Load();
        var summary = config.Tap.IsConfigured ? config.Tap.Describe() : "not set up yet";

        // NotifyIcon.Text is capped at 63 characters.
        var text = $"AntiPilot — {summary}";
        _icon.Text = text.Length > 63 ? text[..60] + "…" : text;

        _actionItem.Enabled = config.Tap.IsConfigured;
        _actionItem.Text = config.Tap.IsConfigured ? $"Run: {summary}" : "Nothing configured";

        var startup = TrayStartup.GetState();
        _startupItem.Checked = startup == TrayStartup.Availability.On;
        _startupItem.Enabled = startup != TrayStartup.Availability.Unavailable;
        _startupItem.Text = startup == TrayStartup.Availability.BlockedByUser
            ? "Start when I sign in (off in Task Manager)"
            : "Start when I sign in";
    }

    private void ToggleStartup()
    {
        var state = TrayStartup.GetState();
        if (state == TrayStartup.Availability.On)
        {
            TrayStartup.Disable();
        }
        else if (TrayStartup.Enable() == TrayStartup.Availability.BlockedByUser)
        {
            MessageBox.Show(
                "Windows remembers that this was switched off in Task Manager, and only you can " +
                "switch it back on: Task Manager → Startup apps → \"AntiPilot tray icon\".",
                "AntiPilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        RefreshLabels();
    }

    private void OpenSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            if (_settings.WindowState == FormWindowState.Minimized)
            {
                _settings.WindowState = FormWindowState.Normal;
            }

            _settings.Activate();
            return;
        }

        _settings = new SettingsForm();
        _settings.FormClosed += (_, _) =>
        {
            _settings = null;

            if (_exitWhenSettingsClose)
            {
                Log.Write("Tray icon closed.");
                ExitThread();
                return;
            }

            RefreshLabels();
        };

        _settings.Show();
        _settings.Activate();
    }

    private static void ShowAbout()
    {
        using var dialog = new AboutDialog();
        dialog.StartPosition = FormStartPosition.CenterScreen;
        dialog.ShowDialog();
    }

    private void RunConfiguredAction()
    {
        var config = AppConfig.Load();
        if (config.Tap.Kind == ActionKind.MenuKey)
        {
            // Pointless from here: the menu would open on the tray menu itself.
            MessageBox.Show(
                "The Menu key action only makes sense when you press the key yourself.",
                "AntiPilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ActionRunner.Run(config.Tap);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _current = null;
            _exitWait?.Unregister(null);
            _exitSignal?.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
            _marshaller.Dispose();
            _settings?.Dispose();
            _singleInstance?.ReleaseMutex();
            _singleInstance?.Dispose();
            _singleInstance = null;
        }

        base.Dispose(disposing);
    }
}

