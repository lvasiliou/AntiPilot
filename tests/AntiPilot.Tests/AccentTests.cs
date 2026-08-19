using AntiPilot.UI;
using Xunit;

namespace AntiPilot.Tests;

public class AccentTests
{
    /// <summary>
    /// A real AccentPalette, read from a machine set to the default Windows blue (#0078D4).
    /// Eight colours, lightest first, each one RGBA.
    /// </summary>
    private static byte[] DefaultBluePalette =>
    [
        0x99, 0xEB, 0xFF, 0xFF, // Light3
        0x4C, 0xC2, 0xFF, 0xFF, // Light2
        0x00, 0x91, 0xF8, 0xFF, // Light1
        0x00, 0x78, 0xD4, 0xFF, // base
        0x00, 0x67, 0xC0, 0xFF, // Dark1
        0x00, 0x3E, 0x92, 0xFF, // Dark2
        0x00, 0x1A, 0x68, 0xFF, // Dark3
        0xF7, 0x63, 0x0C, 0xFF, // unrelated to the accent ramp
    ];

    [Fact]
    public void DarkThemeUsesTheLightVariant()
    {
        var accent = Theme.AccentFromPalette(DefaultBluePalette, isDark: true);
        Assert.Equal(Color.FromArgb(0x4C, 0xC2, 0xFF), accent);
    }

    [Fact]
    public void LightThemeUsesTheDarkVariant()
    {
        var accent = Theme.AccentFromPalette(DefaultBluePalette, isDark: false);
        Assert.Equal(Color.FromArgb(0x00, 0x67, 0xC0), accent);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChannelsAreNotReversed(bool isDark)
    {
        // The bug this guards is silent: reversing R and B leaves a perfectly valid colour, so the
        // only symptom is that the whole UI comes up in the mirror image of the user's accent.
        // Windows' default blue reversed is the amber below, which is what shipped once.
        var accent = Theme.AccentFromPalette(DefaultBluePalette, isDark);

        Assert.True(accent.B > accent.R, $"Expected a blue accent, got #{accent.R:X2}{accent.G:X2}{accent.B:X2}.");
        Assert.NotEqual(Color.FromArgb(0xFF, 0xC2, 0x4C), accent);
        Assert.NotEqual(Color.FromArgb(0xC0, 0x67, 0x00), accent);
    }

    [Fact]
    public void TheTwoShadesDifferByTheme()
    {
        // Picking one shade for both themes would leave text on the accent unreadable in one of them.
        Assert.NotEqual(
            Theme.AccentFromPalette(DefaultBluePalette, isDark: true),
            Theme.AccentFromPalette(DefaultBluePalette, isDark: false));
    }
}
