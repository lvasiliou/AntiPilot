using System.Diagnostics;
using System.Globalization;
using AntiPilot.Interop;
using AntiPilot.UI;

namespace AntiPilot;

internal static class Program
{
    /// <summary>Must match the uap:Protocol name in AppxManifest.xml.</summary>
    public const string ProtocolScheme = "antipilot-key";

    private enum Mode
    {
        /// <summary>Do what the Copilot key is configured to do.</summary>
        KeyPress,

        /// <summary>Show the settings window.</summary>
        Settings,

        /// <summary>Sit in the notification area.</summary>
        Tray,
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var activationUri = args.FirstOrDefault(a =>
            a.StartsWith(ProtocolScheme + ":", StringComparison.OrdinalIgnoreCase));

        var mode = ResolveMode(args, activationUri);
        var config = AppConfig.Load();
        ApplyLanguage(config);

        switch (mode)
        {
            case Mode.Settings:
                ShowSettings();
                return 0;

            case Mode.Tray:
                RunTray();
                return 0;

            default:
                // Windows launches the Copilot key provider by AUMID with no arguments at all;
                // the documented URI activation only shows up on some machines. Both land here.
                return HandleKeyPress(config, activationUri is null ? "Tap" : GetQueryValue(activationUri, "state") ?? "Tap");
        }
    }

    /// <summary>
    /// Honours the language chosen in settings. Done before any window exists, because a form reads
    /// its strings as it is built.
    /// </summary>
    private static void ApplyLanguage(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Language))
        {
            return;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(config.Language!);
            Strings.Culture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            Log.Write($"Configured language '{config.Language}' is not a culture Windows knows; using the system one.");
        }
    }

    private static Mode ResolveMode(string[] args, string? activationUri)
    {
        if (Match(args, "--settings", "/settings"))
        {
            return Mode.Settings;
        }

        if (Match(args, "--tray", "/tray"))
        {
            return Mode.Tray;
        }

        if (activationUri is not null || Match(args, "--key", "/key"))
        {
            return Mode.KeyPress;
        }

        // The package declares three <Application> entries that share this executable, so the
        // AUMID we were activated under says which one the user (or Windows) started.
        var aumid = NativeMethods.GetCurrentApplicationUserModelId();
        if (aumid is not null)
        {
            if (aumid.EndsWith("!Settings", StringComparison.OrdinalIgnoreCase))
            {
                return Mode.Settings;
            }

            if (aumid.EndsWith("!Tray", StringComparison.OrdinalIgnoreCase))
            {
                return Mode.Tray;
            }
        }

        return Mode.KeyPress;

        static bool Match(string[] values, params string[] wanted) =>
            values.Any(v => wanted.Any(w => v.Equals(w, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// The key path. No window is created here, so focus stays where the user left it.
    /// </summary>
    private static int HandleKeyPress(AppConfig config, string state)
    {
        Log.Write($"Key press: state={state}, aumid={NativeMethods.GetCurrentApplicationUserModelId() ?? "(unpackaged)"}");

        // A long press arrives as Down then Up when Windows uses URI activation, so act on Down and
        // ignore Up to avoid running twice.
        if (state.Equals("Up", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        // Read who is in front before anything else: the double-press wait below takes no focus,
        // but the answer belongs to the moment the key was pressed.
        var foreground = TryGetForegroundProcess();

        // Nothing to detect unless a double press has somewhere to go, and detecting costs every
        // single press the width of the window — so this stays switched off until it is asked for.
        if (config.DoubleTapEnabled && config.DoubleTap.IsConfigured)
        {
            switch (TapCoordinator.Classify(config.DoubleTapWindowMs))
            {
                case TapCoordinator.Press.Handled:
                    return 0;

                case TapCoordinator.Press.Double:
                    RunAction(config, config.DoubleTap);
                    return 0;
            }
        }

        RunAction(config, config.ResolveTap(foreground));
        return 0;
    }

    private static string? TryGetForegroundProcess()
    {
        try
        {
            var name = WindowFinder.GetForegroundProcessName();
            Log.Write($"Foreground app: {name ?? "(none)"}");
            return name;
        }
        catch (Exception ex)
        {
            Log.Write($"Could not read the foreground app: {ex.Message}");
            return null;
        }
    }

    private static void RunAction(AppConfig config, KeyAction action)
    {
        if (!action.IsConfigured)
        {
            // Nothing set up yet — the friendliest thing to do is show the settings window.
            Log.Write("No action configured; opening settings.");
            ShowSettings();
            return;
        }

        ActionRunner.Run(action, ActionFeedback.Balloon, config);
    }

    private static void ShowSettings()
    {
        if (TryFocusExistingWindow())
        {
            return;
        }

        WinFormsHost.Ensure();
        Application.Run(new SettingsForm());
    }

    private static void RunTray()
    {
        if (TrayApplication.IsRunningElsewhere())
        {
            Log.Write("Tray icon is already running; exiting.");
            return;
        }

        WinFormsHost.Ensure();
        Application.Run(new TrayApplication());
    }

    /// <summary>
    /// A key press with nothing configured opens settings; without this, holding the key down
    /// would stack up windows.
    /// </summary>
    private static bool TryFocusExistingWindow()
    {
        int self = Environment.ProcessId;

        foreach (var process in Process.GetProcessesByName("AntiPilot"))
        {
            using (process)
            {
                if (process.Id == self)
                {
                    continue;
                }

                var handle = process.MainWindowHandle;
                if (handle == 0)
                {
                    continue;
                }

                if (NativeMethods.IsIconic(handle))
                {
                    NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
                }

                NativeMethods.SetForegroundWindow(handle);
                return true;
            }
        }

        return false;
    }

    private static string? GetQueryValue(string uri, string name)
    {
        int q = uri.IndexOf('?');
        if (q < 0 || q == uri.Length - 1)
        {
            return null;
        }

        foreach (var pair in uri[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq > 0 && pair[..eq].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return null;
    }
}
