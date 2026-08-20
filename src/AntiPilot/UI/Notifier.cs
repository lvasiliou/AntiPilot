namespace AntiPilot.UI;

/// <summary>
/// Tells the user something went wrong without stealing focus.
///
/// The key-press path has no window and is not supposed to create one: the user pressed a key
/// expecting an app to appear, and a modal dialog landing on top of whatever they were typing into
/// is worse than the failure it is reporting. A notification-area balloon says the same thing and
/// takes focus from nobody.
/// </summary>
internal static class Notifier
{
    private const int BalloonMilliseconds = 8000;

    public static void ShowError(string title, string body, ActionFeedback feedback)
    {
        if (feedback == ActionFeedback.Dialog)
        {
            MessageBox.Show($"{title}\r\n\r\n{body}", Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            ShowBalloon(title, body);
        }
        catch (Exception ex)
        {
            Log.Write($"Could not show the balloon ('{title}'): {ex.Message}");
        }
    }

    private static void ShowBalloon(string title, string body)
    {
        // Same reason as the palette: on the key-press path nothing has set WinForms up yet.
        WinFormsHost.Ensure();

        var icon = new NotifyIcon
        {
            Icon = AppIcon.Load(32),
            Visible = true,
            BalloonTipIcon = ToolTipIcon.Warning,
            BalloonTipTitle = title,
            BalloonTipText = body,
        };

        icon.ShowBalloonTip(BalloonMilliseconds);

        // Inside the tray process there is already a message loop; starting another would nest one
        // inside it. Let the existing loop pump the balloon and tidy up on a timer instead.
        if (Application.MessageLoop)
        {
            var timer = new System.Windows.Forms.Timer { Interval = BalloonMilliseconds };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                icon.Visible = false;
                icon.Dispose();
            };

            timer.Start();
            return;
        }

        // A key-press process would otherwise exit before the balloon had been drawn, so pump
        // messages until it has been seen or has timed out.
        RunUntilDismissed(icon);
    }

    private static void RunUntilDismissed(NotifyIcon icon)
    {
        var context = new ApplicationContext();

        void Finish()
        {
            icon.Visible = false;
            icon.Dispose();
            context.ExitThread();
        }

        icon.BalloonTipClosed += (_, _) => Finish();
        icon.BalloonTipClicked += (_, _) => Finish();

        // BalloonTipClosed does not fire when Windows never showed the balloon at all — quiet
        // hours, focus assist, notifications switched off — so this timer is the real guarantee
        // that the process ends.
        var timeout = new System.Windows.Forms.Timer { Interval = BalloonMilliseconds + 2000 };
        timeout.Tick += (_, _) =>
        {
            timeout.Stop();
            Finish();
        };

        timeout.Start();

        try
        {
            Application.Run(context);
        }
        finally
        {
            timeout.Dispose();
            context.Dispose();
        }
    }
}
