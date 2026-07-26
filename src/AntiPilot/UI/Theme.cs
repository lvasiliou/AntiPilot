using Microsoft.Win32;

namespace AntiPilot.UI;

/// <summary>
/// Follows the Windows app theme. WinForms handles the standard controls and the title bar
/// through Application.SetColorMode; these colours cover everything drawn by hand.
/// </summary>
public static class Theme
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Set ANTIPILOT_COLORMODE to "dark" or "light" to ignore the system setting.</summary>
    private static string? Override =>
        Environment.GetEnvironmentVariable("ANTIPILOT_COLORMODE")?.Trim().ToLowerInvariant();

    public static bool IsDark
    {
        get
        {
            switch (Override)
            {
                case "dark":
                    return true;
                case "light":
                    return false;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static Color Window => IsDark ? Color.FromArgb(0x20, 0x20, 0x20) : Color.White;

    public static Color Card => IsDark ? Color.FromArgb(0x2D, 0x2D, 0x2D) : Color.FromArgb(0xF3, 0xF5, 0xF9);

    public static Color Text => IsDark ? Color.FromArgb(0xF0, 0xF0, 0xF0) : SystemColors.ControlText;

    public static Color SecondaryText => IsDark ? Color.FromArgb(0xA6, 0xA6, 0xA6) : SystemColors.GrayText;

    public static Color ListBackground => IsDark ? Color.FromArgb(0x2B, 0x2B, 0x2B) : Color.White;

    /// <summary>Sets up WinForms' own dark mode. Call once before creating any window.</summary>
    public static void Apply()
    {
#pragma warning disable WFO5001 // SetColorMode is still marked experimental in .NET 10.
        var mode = Override switch
        {
            "dark" => SystemColorMode.Dark,
            "light" => SystemColorMode.Classic,
            _ => SystemColorMode.System,
        };

        Application.SetColorMode(mode);
#pragma warning restore WFO5001
    }
}
