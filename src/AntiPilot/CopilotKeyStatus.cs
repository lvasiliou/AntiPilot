using System.Diagnostics;
using AntiPilot.Interop;
using Microsoft.Win32;

namespace AntiPilot;

/// <summary>
/// Reads what Windows currently does with the Copilot key. Documented at
/// https://learn.microsoft.com/windows/apps/develop/windows-integration/microsoft-copilot-key-provider
/// </summary>
public static class CopilotKeyStatus
{
    private const string KeyPath = @"Software\Microsoft\Windows\Shell\BrandedKey";

    /// <summary>Application id of the &lt;Application&gt; element in AppxManifest.xml.</summary>
    public const string ApplicationId = "AntiPilot";

    /// <summary>"Search", "App", "AppEnforcedByPolicy", or null when the user never changed it.</summary>
    public static string? ChoiceType => ReadValue("BrandedKeyChoiceType");

    /// <summary>AUMID of the app last configured as the key target, even when the key is set to Search.</summary>
    public static string? TargetAumid => ReadValue("AppAumid");

    /// <summary>Our own AUMID, or null when running outside the MSIX package.</summary>
    public static string? OwnAumid
    {
        get
        {
            var family = NativeMethods.GetCurrentPackageFamilyName();
            return family is null ? null : $"{family}!{ApplicationId}";
        }
    }

    public static bool IsPackaged => NativeMethods.GetCurrentPackageFamilyName() is not null;

    /// <summary>True when the Copilot key is currently wired up to this app.</summary>
    public static bool IsActiveTarget
    {
        get
        {
            var own = OwnAumid;
            if (own is null)
            {
                return false;
            }

            var choice = ChoiceType;
            if (!string.Equals(choice, "App", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(choice, "AppEnforcedByPolicy", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(TargetAumid, own, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string Describe()
    {
        if (!IsPackaged)
        {
            return "Debug build — Windows only sees the installed MSIX.";
        }

        if (IsActiveTarget)
        {
            return "The Copilot key and Win+C run AntiPilot.";
        }

        return ChoiceType switch
        {
            "Search" => "The Copilot key opens Search — pick AntiPilot →",
            "App" or "AppEnforcedByPolicy" => "The Copilot key opens another app — pick AntiPilot →",
            _ => "The Copilot key still does its default thing →",
        };
    }

    /// <summary>
    /// Opens the "Customize Copilot key on keyboard" setting. It lives under
    /// Bluetooth &amp; devices &gt; Keyboard &gt; Shortcuts and hotkeys on current builds — the page id is
    /// SettingsPageDevicesKeyboard — but the deep link below still carries its old personalization name.
    /// </summary>
    public static void OpenWindowsSettings()
    {
        string[] candidates =
        [
            "ms-settings:personalization-textinput-copilot-hardwarekey", // straight to the dropdown
            "ms-settings:keyboard",                                      // the page it sits on
            "ms-settings:personalization-textinput",                     // where it used to live
        ];

        foreach (var uri in candidates)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true })?.Dispose();
                return;
            }
            catch (Exception ex)
            {
                Log.Write($"Could not open {uri}: {ex.Message}");
            }
        }
    }

    private static string? ReadValue(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(name) as string;
        }
        catch (Exception ex)
        {
            Log.Write($"Could not read {name}: {ex.Message}");
            return null;
        }
    }
}
