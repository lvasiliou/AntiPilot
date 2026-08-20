using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AntiPilot.UI;

/// <summary>
/// The design tokens the whole UI is drawn from: a Windows 11 (Fluent) palette in both themes,
/// plus the user's own accent colour.
///
/// The values are the ones WinUI uses, transcribed rather than invented — a card in light mode is
/// #FBFBFB on a #F3F3F3 page because that is what Windows Settings is, and matching it is the whole
/// point. WinForms draws none of this for us, so everything here is applied by hand.
/// </summary>
public static class Theme
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const string DwmKey = @"Software\Microsoft\Windows\DWM";

    private const string AccentKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    /// <summary>Set ANTIPILOT_COLORMODE to "dark" or "light" to ignore the system setting.</summary>
    private static string? Override =>
        Environment.GetEnvironmentVariable("ANTIPILOT_COLORMODE")?.Trim().ToLowerInvariant();

    /// <summary>
    /// Cached because it is read once per painted control, and because it has to stay stable for
    /// the duration of a repaint even if the user flips the system setting halfway through.
    /// </summary>
    private static bool? _isDark;

    private static Color? _accent;

    public static bool IsDark => _isDark ??= ReadIsDark();

    /// <summary>Drops the cached theme and accent so the next read picks up a system change.</summary>
    public static void Invalidate()
    {
        _isDark = null;
        _accent = null;
    }

    private static bool ReadIsDark()
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

    // ---- surfaces ----------------------------------------------------------

    /// <summary>The page behind everything. Windows Settings puts Mica here; see <see cref="Backdrop"/>.</summary>
    public static Color Window => IsDark ? Color.FromArgb(0x20, 0x20, 0x20) : Color.FromArgb(0xF3, 0xF3, 0xF3);

    /// <summary>A card sitting on the page.</summary>
    public static Color Card => IsDark ? Color.FromArgb(0x2B, 0x2B, 0x2B) : Color.FromArgb(0xFB, 0xFB, 0xFB);

    /// <summary>The hairline around a card. Doing the work a shadow would in a heavier design.</summary>
    public static Color CardStroke => IsDark ? Color.FromArgb(0x35, 0x35, 0x35) : Color.FromArgb(0xE5, 0xE5, 0xE5);

    /// <summary>Fill of an interactive control — a text box, a combo, a standard button.</summary>
    public static Color ControlFill => IsDark ? Color.FromArgb(0x2D, 0x2D, 0x2D) : Color.White;

    public static Color ControlStroke => IsDark ? Color.FromArgb(0x3A, 0x3A, 0x3A) : Color.FromArgb(0xDF, 0xDF, 0xDF);

    /// <summary>Wash under a hovered row. Deliberately barely there.</summary>
    public static Color SubtleHover => IsDark ? Color.FromArgb(0x33, 0x33, 0x33) : Color.FromArgb(0xED, 0xED, 0xED);

    public static Color SubtlePressed => IsDark ? Color.FromArgb(0x2A, 0x2A, 0x2A) : Color.FromArgb(0xE3, 0xE3, 0xE3);

    public static Color ListBackground => IsDark ? Color.FromArgb(0x2B, 0x2B, 0x2B) : Color.White;

    // ---- text --------------------------------------------------------------

    public static Color Text => IsDark ? Color.FromArgb(0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1A, 0x1A, 0x1A);

    public static Color SecondaryText => IsDark ? Color.FromArgb(0xC5, 0xC5, 0xC5) : Color.FromArgb(0x5D, 0x5D, 0x5D);

    public static Color DisabledText => IsDark ? Color.FromArgb(0x7A, 0x7A, 0x7A) : Color.FromArgb(0x9D, 0x9D, 0x9D);

    // ---- accent ------------------------------------------------------------

    /// <summary>
    /// The user's accent colour, in the shade WinUI would use against the current theme: the light
    /// variant on dark backgrounds and the dark variant on light ones, so text on top of it stays
    /// readable whatever colour the user picked.
    /// </summary>
    public static Color Accent => _accent ??= ReadAccent();

    public static Color AccentHover => Blend(Accent, IsDark ? Color.Black : Color.White, 0.10);

    public static Color AccentPressed => Blend(Accent, IsDark ? Color.Black : Color.White, 0.20);

    /// <summary>Text drawn on top of <see cref="Accent"/>.</summary>
    public static Color AccentText => IsDark ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White;

    /// <summary>Offset into AccentPalette of the shade WinUI fills accent surfaces with.</summary>
    private const int AccentPaletteLight2 = 4;   // dark theme

    private const int AccentPaletteDark1 = 16;   // light theme

    /// <summary>
    /// Picks the accent shade out of the raw AccentPalette blob.
    ///
    /// Eight colours, lightest first — Light3, Light2, Light1, base, Dark1, Dark2, Dark3 — and each
    /// one is stored **RGBA**, not BGRA. That is worth stating loudly because getting it backwards
    /// is silent: every channel is still a valid colour, so the UI simply comes up in the mirror
    /// image of the user's accent. The default Windows blue #0078D4 reversed is #D47800, an amber,
    /// which is exactly what shipped until someone asked why the buttons were yellow. The DWM
    /// AccentColor DWORD in <see cref="ReadAccent"/> genuinely is ABGR; only this blob is not.
    /// </summary>
    internal static Color AccentFromPalette(byte[] palette, bool isDark)
    {
        int offset = isDark ? AccentPaletteLight2 : AccentPaletteDark1;
        return Color.FromArgb(palette[offset], palette[offset + 1], palette[offset + 2]);
    }

    private static Color ReadAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AccentKey);
            if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 32)
            {
                return AccentFromPalette(palette, IsDark);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Could not read the accent palette: {ex.Message}");
        }

        // DWM stores the accent as ABGR, which is the same bytes in the other order.
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
            if (key?.GetValue("AccentColor") is int abgr)
            {
                return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Could not read the DWM accent colour: {ex.Message}");
        }

        // WinUI's own defaults, for a machine that has never been personalised.
        return IsDark ? Color.FromArgb(0x60, 0xCD, 0xFF) : Color.FromArgb(0x00, 0x5F, 0xB8);
    }

    /// <summary>Mixes <paramref name="amount"/> of <paramref name="towards"/> into a colour.</summary>
    public static Color Blend(Color from, Color towards, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            from.A,
            (int)(from.R + (towards.R - from.R) * amount),
            (int)(from.G + (towards.G - from.G) * amount),
            (int)(from.B + (towards.B - from.B) * amount));
    }

    // ---- metrics -----------------------------------------------------------

    /// <summary>Corner radius of a card. Windows 11 uses 8 for containers, 4 for controls.</summary>
    public const int CardRadius = 8;

    public const int ControlRadius = 4;

    /// <summary>Gap between cards inside one group. Windows Settings stacks them tightly.</summary>
    public const int CardGap = 4;

    /// <summary>Gap between groups of cards.</summary>
    public const int GroupGap = 22;

    public const int PagePadding = 20;

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

    /// <summary>True when the chosen language is written right to left.</summary>
    public static bool IsRightToLeft =>
        (Strings.Culture ?? System.Globalization.CultureInfo.CurrentUICulture).TextInfo.IsRightToLeft;

    /// <summary>
    /// Turns a window right to left when the chosen language is.
    ///
    /// Note what is deliberately *not* set here: RightToLeftLayout. It works by putting
    /// WS_EX_LAYOUTRTL on the window, which mirrors the whole device context — and everything in
    /// the Fluent folder paints itself into that context, so every card title and button label came
    /// back reversed, letter by letter. Screenshotting the Arabic build is the only reason that was
    /// noticed. RightToLeft on its own flips the standard controls, the scrollbars and the message
    /// boxes without touching what we draw; the custom controls read
    /// <see cref="IsRightToLeft"/> and mirror themselves by hand instead.
    /// </summary>
    public static void ApplyDirection(Form form)
    {
        if (!IsRightToLeft)
        {
            return;
        }

        form.RightToLeft = RightToLeft.Yes;
    }

    /// <summary>
    /// Keeps <paramref name="form"/> in step with the system theme and accent while it is open.
    ///
    /// Windows announces the change and the form recolours itself in place, which is what the user
    /// expects. It is not quite a fresh start-up: a handful of standard controls pick their colours
    /// when their handle is created and only fully catch up next time the window opens. Everything
    /// drawn by the Fluent controls in this folder repaints exactly.
    /// </summary>
    public static void Watch(Form form)
    {
        ApplyDirection(form);

        void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
            {
                return;
            }

            bool wasDark = IsDark;
            var wasAccent = Accent;
            Invalidate();

            if (wasDark == IsDark && wasAccent == Accent)
            {
                return;
            }

            try
            {
                if (form.IsDisposed)
                {
                    return;
                }

                form.BeginInvoke(() =>
                {
                    Apply();
                    ApplyTitleBar(form);
                    Retheme(form);
                    form.Invalidate(invalidateChildren: true);
                    Log.Write($"System theme changed; repainted as {(IsDark ? "dark" : "light")}.");
                });
            }
            catch (Exception)
            {
                // The window went away between the check and the call.
            }
        }

        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        form.FormClosed += (_, _) => SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;

        form.HandleCreated += (_, _) => ApplyTitleBar(form);
        if (form.IsHandleCreated)
        {
            ApplyTitleBar(form);
        }
    }

    /// <summary>
    /// The title bar is drawn by the desktop window manager, not by us, so it needs telling
    /// separately — otherwise a dark window keeps a white caption.
    /// </summary>
    public static void ApplyTitleBar(Form form)
    {
        try
        {
            int dark = IsDark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }
        catch (Exception ex)
        {
            Log.Write($"Could not set the title bar theme: {ex.Message}");
        }
    }

    /// <summary>Re-applies the hand-picked colours over a control tree after a theme change.</summary>
    public static void Retheme(Control root)
    {
        // The Fluent controls read the tokens as they paint, so they need nothing but an invalidate.
        if (root is IThemedControl themed)
        {
            themed.OnThemeChanged();
        }
        else
        {
            switch (root)
            {
                case ListBox or ListView:
                    root.BackColor = ListBackground;
                    root.ForeColor = Text;
                    break;

                case TextBox:
                    root.BackColor = ControlFill;
                    root.ForeColor = Text;
                    break;

                case LinkLabel link:
                    link.ForeColor = Text;
                    link.LinkColor = Accent;
                    break;

                case Label label:
                    label.ForeColor = ReferenceEquals(label.Tag, SecondaryTag) ? SecondaryText : Text;
                    break;

                case Button:
                    break;

                default:
                    root.BackColor = ReferenceEquals(root.Tag, CardTag) ? Card : Window;
                    root.ForeColor = Text;
                    break;
            }
        }

        foreach (Control child in root.Controls)
        {
            Retheme(child);
        }
    }

    /// <summary>Marks a label as secondary so <see cref="Retheme"/> can keep it grey.</summary>
    public static readonly object SecondaryTag = new();

    /// <summary>Marks a panel as a card so <see cref="Retheme"/> can keep its raised background.</summary>
    public static readonly object CardTag = new();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Asks the desktop window manager for the Mica backdrop and rounded corners.
    ///
    /// Both are ignored on builds that do not know the attribute, which is why neither is checked:
    /// DwmSetWindowAttribute simply returns a failure HRESULT on Windows 10 and the window looks
    /// exactly as it did before.
    /// </summary>
    internal static void ApplyWindowEffects(Form form, bool mica)
    {
        try
        {
            int round = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

            int backdrop = mica ? 2 : 1; // DWMSBT_MAINWINDOW : DWMSBT_NONE
            DwmSetWindowAttribute(form.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        catch (Exception ex)
        {
            Log.Write($"Could not set the window backdrop: {ex.Message}");
        }
    }
}

/// <summary>
/// Implemented by the controls that paint themselves from <see cref="Theme"/>, so a theme change
/// only has to tell them to repaint rather than reach in and set colours on them.
/// </summary>
internal interface IThemedControl
{
    void OnThemeChanged();
}
