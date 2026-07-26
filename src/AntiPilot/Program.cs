using System.Diagnostics;
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
                return HandleKeyPress(activationUri is null ? "Tap" : GetQueryValue(activationUri, "state") ?? "Tap");
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
    private static int HandleKeyPress(string state)
    {
        var config = AppConfig.Load();
        Log.Write($"Key press: state={state}, aumid={NativeMethods.GetCurrentApplicationUserModelId() ?? "(unpackaged)"}");

        // Every press does the same thing. A long press arrives as Down then Up when Windows uses
        // URI activation, so act on Down and ignore Up to avoid running twice.
        if (state.Equals("Up", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        RunAction(config);
        return 0;
    }

    private static void RunAction(AppConfig config)
    {
        if (!config.Tap.IsConfigured)
        {
            // Nothing set up yet — the friendliest thing to do is show the settings window.
            Log.Write("No action configured; opening settings.");
            ShowSettings();
            return;
        }

        ActionRunner.Run(config.Tap);
    }

    private static void ShowSettings()
    {
        if (TryFocusExistingWindow())
        {
            return;
        }

        InitialiseWinForms();
        Application.Run(new SettingsForm());
    }

    private static void RunTray()
    {
        if (TrayApplication.IsRunningElsewhere())
        {
            Log.Write("Tray icon is already running; exiting.");
            return;
        }

        InitialiseWinForms();
        Application.Run(new TrayApplication());
    }

    private static void InitialiseWinForms()
    {
        ApplicationConfiguration.Initialize();
        Theme.Apply();
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
