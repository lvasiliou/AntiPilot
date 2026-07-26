using System.Reflection;
using AntiPilot.Interop;
using Microsoft.Win32;

namespace AntiPilot;

/// <summary>
/// Runs the tray icon at sign-in via a shortcut in the user's Startup folder.
///
/// The tidier MSIX route, Windows.ApplicationModel.StartupTask, is unusable here: it resolves the
/// task against the &lt;Application&gt; entry that is running, and the tray's entry is a different one
/// from the settings window's, so the settings window can only ever get "task not found". A Startup
/// shortcut is visible in the same Task Manager list, works from every entry point, and — verified
/// on this machine — is not caught by MSIX's AppData redirection.
/// </summary>
public static class TrayStartup
{
    private const string ShortcutName = "AntiPilot tray icon.lnk";

    private const string ApprovalKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public enum Availability
    {
        /// <summary>No package identity, so there is no tray entry point to point a shortcut at.</summary>
        Unavailable,

        On,
        Off,

        /// <summary>Switched off in Task Manager; only the user can undo that, in the same place.</summary>
        BlockedByUser,
    }

    private static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);

    private static string? TrayAumid
    {
        get
        {
            var family = NativeMethods.GetCurrentPackageFamilyName();
            return family is null ? null : $"{family}!Tray";
        }
    }

    public static Availability GetState()
    {
        if (TrayAumid is null)
        {
            return Availability.Unavailable;
        }

        if (!File.Exists(ShortcutPath))
        {
            return Availability.Off;
        }

        return IsApprovedByUser() ? Availability.On : Availability.BlockedByUser;
    }

    public static Availability Enable()
    {
        var aumid = TrayAumid;
        if (aumid is null)
        {
            return Availability.Unavailable;
        }

        try
        {
            // explorer.exe + AUMID rather than the exe path: the install path carries the package
            // version, so a direct path would break on every update.
            var explorer = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

            CreateShortcut(
                ShortcutPath,
                explorer,
                $"shell:AppsFolder\\{aumid}",
                "Starts the AntiPilot notification-area icon.",
                Environment.ProcessPath);

            Log.Write($"Startup shortcut written to {ShortcutPath}");
        }
        catch (Exception ex)
        {
            Log.Write($"Could not create the startup shortcut: {ex}");
            return Availability.Off;
        }

        return GetState();
    }

    public static Availability Disable()
    {
        try
        {
            var path = ShortcutPath;
            Log.Write($"Removing startup shortcut: exists={File.Exists(path)} path={path}");

            if (File.Exists(path))
            {
                File.Delete(path);
                Log.Write($"Startup shortcut removed (still there: {File.Exists(path)}).");
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Could not remove the startup shortcut: {ex}");
        }

        return GetState();
    }

    /// <summary>
    /// Task Manager records its own verdict next to the shortcut; a leading 2 means enabled,
    /// 3 means the user switched it off there and Explorer will ignore the shortcut.
    /// </summary>
    private static bool IsApprovedByUser()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovalKey);
            if (key?.GetValue(ShortcutName) is byte[] { Length: > 0 } value)
            {
                return (value[0] & 1) == 0;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Could not read the startup approval state: {ex.Message}");
        }

        return true; // No entry means Explorer has not been told to disable it.
    }

    private static void CreateShortcut(
        string path, string target, string arguments, string description, string? iconSource)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is not available.");

        object? shell = Activator.CreateInstance(shellType);
        if (shell is null)
        {
            throw new InvalidOperationException("Could not create WScript.Shell.");
        }

        try
        {
            object? link = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell, [path]);
            if (link is null)
            {
                throw new InvalidOperationException("Could not create the shortcut object.");
            }

            var linkType = link.GetType();
            Set(linkType, link, "TargetPath", target);
            Set(linkType, link, "Arguments", arguments);
            Set(linkType, link, "Description", description);

            if (!string.IsNullOrEmpty(iconSource))
            {
                Set(linkType, link, "IconLocation", $"{iconSource},0");
            }

            linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }

        static void Set(Type type, object instance, string property, string value) =>
            type.InvokeMember(property, BindingFlags.SetProperty, null, instance, [value]);
    }
}
