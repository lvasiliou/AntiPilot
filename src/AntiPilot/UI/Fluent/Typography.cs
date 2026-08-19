using System.Drawing.Text;

namespace AntiPilot.UI.Fluent;

/// <summary>
/// The WinUI type ramp, in the fonts Windows 11 actually uses.
///
/// Segoe UI Variable comes with Windows 11 in three optical sizes — Display for headings, Text for
/// body copy, Small for the tiny stuff — and using the right one is most of what makes a window
/// look like it belongs. Windows 10 has none of them, so everything falls back to Segoe UI, which
/// is what that machine expects to see anyway.
///
/// Sizes are converted from the WinUI ramp, which is quoted in pixels: 14px body becomes 10.5pt at
/// 96 DPI, and WinForms scales from there.
/// </summary>
internal static class Typography
{
    private static readonly string DisplayFamily = FirstAvailable("Segoe UI Variable Display", "Segoe UI");
    private static readonly string TextFamily = FirstAvailable("Segoe UI Variable Text", "Segoe UI");
    private static readonly string SmallFamily = FirstAvailable("Segoe UI Variable Small", "Segoe UI");

    /// <summary>
    /// The icon font. Windows 11 ships Segoe Fluent Icons; Windows 10 has Segoe MDL2 Assets, and
    /// the glyphs this app uses have the same code points in both.
    /// </summary>
    private static readonly string IconFamily = FirstAvailable("Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");

    /// <summary>28px — the page heading.</summary>
    public static Font Title { get; } = new(DisplayFamily, 21f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>20px — a group heading.</summary>
    public static Font Subtitle { get; } = new(DisplayFamily, 15f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>14px — everything else.</summary>
    public static Font Body { get; } = new(TextFamily, 10.5f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>14px semibold — the title line of a settings card.</summary>
    public static Font BodyStrong { get; } = new(TextFamily, 10.5f, FontStyle.Bold, GraphicsUnit.Point);

    /// <summary>12px — the description line under a card title.</summary>
    public static Font Caption { get; } = new(SmallFamily, 9f, FontStyle.Regular, GraphicsUnit.Point);

    public static Font Icon { get; } = new(IconFamily, 15f, FontStyle.Regular, GraphicsUnit.Point);

    public static Font SmallIcon { get; } = new(IconFamily, 11f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>
    /// Glyphs from the icon font, written as code points rather than pasted characters: they live
    /// in the private use area, so the literal form shows as an empty box in most editors and in
    /// every diff.
    /// </summary>
    public static class Glyphs
    {
        public const string Keyboard = "\uE765";
        public const string Stopwatch = "\uE916";
        public const string AllApps = "\uE71D";
        public const string AppIcon = "\uECAA";
        public const string Settings = "\uE713";
        public const string Ringer = "\uE7E7";
        public const string Language = "\uF2B7";
        public const string Info = "\uE946";
        public const string Page = "\uE7C3";
        public const string Import = "\uE896";
        public const string Export = "\uE898";
        public const string Play = "\uE768";
        public const string ChevronRight = "\uE76C";
        public const string Warning = "\uE7BA";
        public const string Accept = "\uE73E";
    }

    private static string FirstAvailable(params string[] families)
    {
        try
        {
            using var installed = new InstalledFontCollection();
            var names = installed.Families.Select(family => family.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var family in families)
            {
                if (names.Contains(family))
                {
                    return family;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Could not enumerate fonts: {ex.Message}");
        }

        return families[^1];
    }
}
