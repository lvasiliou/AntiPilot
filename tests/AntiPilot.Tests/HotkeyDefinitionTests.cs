using Xunit;

namespace AntiPilot.Tests;

public class HotkeyDefinitionTests
{
    [Theory]
    [InlineData("Ctrl+Shift+Escape")]
    [InlineData("Alt+F4")]
    [InlineData("Win+V")]
    [InlineData("PrintScreen")]
    [InlineData("Ctrl+Alt+Shift+Win+Delete")]
    [InlineData("MediaPlayPause")]
    [InlineData("Num5")]
    [InlineData("F13")]
    [InlineData("Win+.")]
    public void RoundTripsThroughText(string chord)
    {
        Assert.True(HotkeyDefinition.TryParse(chord, out var parsed));
        Assert.Equal(chord, parsed.Format());

        // The formatted form has to parse back to something equal, or config would drift on save.
        Assert.True(HotkeyDefinition.TryParse(parsed.Format(), out var again));
        Assert.Equal(parsed, again);
    }

    [Theory]
    [InlineData("control+shift+ESCAPE", "Ctrl+Shift+Escape")]
    [InlineData("CTRL + ALT + del", "Ctrl+Alt+Delete")]
    [InlineData("meta+e", "Win+E")]
    [InlineData("Esc", "Escape")]
    [InlineData("PgDn", "PageDown")]
    [InlineData("Shift+Ctrl+A", "Ctrl+Shift+A")]
    public void NormalisesAliasesAndOrder(string input, string expected)
    {
        Assert.True(HotkeyDefinition.TryParse(input, out var parsed));
        Assert.Equal(expected, parsed.Format());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Win")]
    [InlineData("NotAKey")]
    [InlineData("Ctrl+A+B")]
    public void RejectsWhatCannotBeSent(string? input)
    {
        Assert.False(HotkeyDefinition.TryParse(input, out _));
    }

    [Fact]
    public void ModifiersAloneAreNeverAShortcut()
    {
        // A chord whose key is itself a modifier would press and release nothing meaningful.
        Assert.True(HotkeyDefinition.IsModifierKey(0x10));
        Assert.True(HotkeyDefinition.IsModifierKey(0x5B));
        Assert.False(HotkeyDefinition.IsModifierKey(0x1B));
    }

    [Theory]
    [InlineData("Left", true)]
    [InlineData("Delete", true)]
    [InlineData("Home", true)]
    [InlineData("VolumeUp", true)]
    [InlineData("A", false)]
    [InlineData("F5", false)]
    [InlineData("Num4", false)]
    public void MarksTheKeysThatNeedTheExtendedPrefix(string chord, bool extended)
    {
        // Arrows and the keypad share virtual-key codes; only this flag tells them apart, so a
        // regression here would silently send Num4 where Left was meant.
        Assert.True(HotkeyDefinition.TryParse(chord, out var parsed));
        Assert.Equal(extended, parsed.IsExtended);
    }

    [Fact]
    public void EqualityIgnoresNothing()
    {
        var baseline = new HotkeyDefinition(0x41, control: true);

        Assert.Equal(baseline, new HotkeyDefinition(0x41, control: true));
        Assert.NotEqual(baseline, new HotkeyDefinition(0x41, control: true, shift: true));
        Assert.NotEqual(baseline, new HotkeyDefinition(0x42, control: true));
    }
}
