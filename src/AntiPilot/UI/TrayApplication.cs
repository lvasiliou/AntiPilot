using AntiPilot.Interop;

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

    /// <summary>Where Windows, and a settings window in another process, ask the icon to go.</summary>
    private readonly TrayWindow _window;
    private EventWaitHandle? _exitSignal;
    private RegisteredWaitHandle? _exitWait;
    private bool _exitWhenSettingsClose;

    /// <summary>Windows sends a close request in three messages; the answer is one exit.</summary>
    private bool _exiting;

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _actionItem;
    private readonly ToolStripMenuItem _startupItem;
    private SettingsForm? _settings;

    public TrayApplication()
    {
        _actionItem = new ToolStripMenuItem(Strings.TrayRunAction, null, (_, _) => RunConfiguredAction());
        _startupItem = new ToolStripMenuItem(Strings.TrayStartWithSignIn, null, (_, _) => ToggleStartup())
        {
            CheckOnClick = false,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(Strings.TraySettings, null, (_, _) => OpenSettings()) { Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add(_actionItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Strings.TrayCustomiseKey, null,
            (_, _) => CopilotKeyStatus.OpenWindowsSettings()));
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripMenuItem(Strings.TrayAbout, null, (_, _) => ShowAbout()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Strings.TrayExit, null, (_, _) => ExitThread()));
        menu.Opening += (_, _) => RefreshLabels();

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Load(32),
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => OpenSettings();

        _window = new TrayWindow(HideAndExit);
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
            var target = _window.Handle;
            _exitWait = ThreadPool.RegisterWaitForSingleObject(
                _exitSignal,
                (_, _) => NativeMethods.PostMessageW(target, TrayWindow.WM_EXIT_REQUEST, 0, 0),
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
        if (_exiting)
        {
            return;
        }

        _exiting = true;
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
            _icon.BalloonTipTitle = Strings.TrayIntroTitle;
            _icon.BalloonTipText = Strings.TrayIntroBody;
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
        var summary = config.Tap.IsConfigured ? config.Tap.Describe() : Strings.TrayNotSetUp;

        // NotifyIcon.Text is capped at 63 characters.
        var text = Strings.Format(Strings.TrayTooltip, summary);
        _icon.Text = text.Length > 63 ? text[..60] + "…" : text;

        _actionItem.Enabled = config.Tap.IsConfigured;
        _actionItem.Text = config.Tap.IsConfigured ? Strings.Format(Strings.TrayRunNamed, summary) : Strings.TrayNothingConfigured;

        var startup = TrayStartup.GetState();
        _startupItem.Checked = startup == TrayStartup.Availability.On;
        _startupItem.Enabled = startup != TrayStartup.Availability.Unavailable;
        _startupItem.Text = startup == TrayStartup.Availability.BlockedByUser
            ? Strings.TrayStartBlocked
            : Strings.TrayStartWithSignIn;
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
                Strings.TrayStartupBlockedBody,
                Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        if (config.Tap.Kind is ActionKind.MenuKey or ActionKind.Hotkey)
        {
            // Pointless from here: synthesised input lands on whatever has focus, which at this
            // moment is the tray menu the user is clicking in.
            MessageBox.Show(
                Strings.TrayMenuKeyOnly,
                Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ActionRunner.Run(config.Tap, ActionFeedback.Dialog, config);
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
            _window.DestroyHandle();
            _settings?.Dispose();
            _singleInstance?.ReleaseMutex();
            _singleInstance?.Dispose();
            _singleInstance = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// The process's own top-level window, never shown. Windows talks to a process through its
    /// top-level windows: before updating or removing the package, and at sign-out, it asks each
    /// one to close and gives the process thirty seconds before killing it and filing a hang
    /// report. A notification icon's window lets those requests fall through to the default
    /// handling, which answers "fine" and then does nothing, so the icon sat there until the
    /// deadline on every update. This window answers by leaving.
    /// </summary>
    private sealed class TrayWindow : NativeWindow
    {
        /// <summary>Posted from the thread that watches the cross-process exit event.</summary>
        public const int WM_EXIT_REQUEST = 0x8000 + 1;

        private const int WM_CLOSE = 0x0010;
        private const int WM_QUERYENDSESSION = 0x0011;
        private const int WM_ENDSESSION = 0x0016;
        private const int ENDSESSION_CLOSEAPP = 0x00000001;

        private readonly Action _close;

        public TrayWindow(Action close)
        {
            _close = close;
            CreateHandle(new CreateParams { Caption = Strings.AppName, ExStyle = NativeMethods.WS_EX_TOOLWINDOW });
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_QUERYENDSESSION:
                    Log.Write((m.LParam & ENDSESSION_CLOSEAPP) != 0
                        ? "Windows is updating or removing the package; the tray icon will close."
                        : "The session is ending; the tray icon will close.");
                    m.Result = 1;
                    return;

                case WM_ENDSESSION:
                    if (m.WParam != 0)
                    {
                        _close();
                    }

                    m.Result = 0;
                    return;

                case WM_CLOSE:
                    _close();
                    m.Result = 0;
                    return;

                case WM_EXIT_REQUEST:
                    _close();
                    m.Result = 0;
                    return;
            }

            base.WndProc(ref m);
        }
    }
}

