using Xunit;

namespace AntiPilot.Tests;

public class ActionValidatorTests
{
    [Fact]
    public void AnUnconfiguredActionHasNothingToComplainAbout()
    {
        Assert.Null(ActionValidator.Validate(new KeyAction()));
    }

    [Fact]
    public void AcceptsAFileThatIsThere()
    {
        var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
        Assert.Null(ActionValidator.Validate(new KeyAction { Kind = ActionKind.File, Path = path }));
    }

    [Fact]
    public void FlagsAFileThatIsNot()
    {
        var path = Path.Combine(Path.GetTempPath(), "definitely-not-here-" + Guid.NewGuid().ToString("N") + ".exe");
        Assert.NotNull(ActionValidator.Validate(new KeyAction { Kind = ActionKind.File, Path = path }));
    }

    [Fact]
    public void AcceptsAFolder()
    {
        Assert.Null(ActionValidator.Validate(new KeyAction { Kind = ActionKind.File, Path = Path.GetTempPath() }));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("ms-settings:keyboard")]
    [InlineData("mailto:someone@example.com")]
    public void TakesLinksOnTrust(string target)
    {
        // Only the shell can say whether a scheme resolves, so calling one of these "missing"
        // would be a warning the user could do nothing about.
        Assert.Null(ActionValidator.Validate(new KeyAction { Kind = ActionKind.File, Path = target }));
    }

    [Fact]
    public void ExpandsEnvironmentVariablesBeforeLooking()
    {
        Assert.Null(ActionValidator.Validate(new KeyAction
        {
            Kind = ActionKind.File,
            Path = @"%WINDIR%\System32\notepad.exe",
        }));
    }

    [Fact]
    public void ResolvesABareCommandAgainstPath()
    {
        // "notepad" with no directory is what a user types, and it launches, so it must not be
        // reported as missing.
        Assert.Null(ActionValidator.Validate(new KeyAction { Kind = ActionKind.File, Path = "notepad" }));
    }

    [Fact]
    public void FlagsAShortcutThatCannotBeSent()
    {
        Assert.NotNull(ActionValidator.Validate(new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "Ctrl+Ctrl" }));
    }

    [Fact]
    public void AcceptsAShortcutThatCan()
    {
        Assert.Null(ActionValidator.Validate(new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "Ctrl+Shift+Escape" }));
    }
}
