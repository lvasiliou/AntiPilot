using Xunit;

namespace AntiPilot.Tests;

public class KeyActionTests
{
    [Fact]
    public void NothingIsNeverConfigured()
    {
        Assert.False(new KeyAction().IsConfigured);
    }

    [Theory]
    [InlineData(ActionKind.ShellApp, false)]
    [InlineData(ActionKind.File, false)]
    [InlineData(ActionKind.Hotkey, false)]
    [InlineData(ActionKind.MenuKey, true)]
    [InlineData(ActionKind.Palette, true)]
    public void AKindWithNoTargetIsNotConfigured(ActionKind kind, bool configured)
    {
        Assert.Equal(configured, new KeyAction { Kind = kind }.IsConfigured);
    }

    [Fact]
    public void AShortcutIsOnlyConfiguredOnceItParses()
    {
        Assert.False(new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "Ctrl" }.IsConfigured);
        Assert.False(new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "nonsense" }.IsConfigured);
        Assert.True(new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "Ctrl+Shift+Escape" }.IsConfigured);
    }

    [Fact]
    public void DescribeNeverReturnsAnEmptyLabel()
    {
        foreach (ActionKind kind in Enum.GetValues<ActionKind>())
        {
            var description = new KeyAction { Kind = kind }.Describe();
            Assert.False(string.IsNullOrWhiteSpace(description));
        }
    }

    [Fact]
    public void AUserSuppliedLabelWins()
    {
        var action = new KeyAction { Kind = ActionKind.MenuKey, Label = "Right-click" };
        Assert.Equal("Right-click", action.Describe());
    }

    [Fact]
    public void DescribeShortensAFilePathToItsName()
    {
        var action = new KeyAction { Kind = ActionKind.File, Path = @"C:\Windows\System32\notepad.exe" };
        Assert.Equal("notepad.exe", action.Describe());
    }

    [Fact]
    public void CloneDoesNotShareState()
    {
        var original = new KeyAction { Kind = ActionKind.File, Path = "a" };
        var copy = original.Clone();
        copy.Path = "b";

        Assert.Equal("a", original.Path);
    }

    [Fact]
    public void ConfigCloneCopiesTheListsToo()
    {
        var original = new AppConfig
        {
            AppRules = [new AppRule { ProcessName = "chrome" }],
            Palette = [new KeyAction { Kind = ActionKind.MenuKey }],
        };

        var copy = original.Clone();
        copy.AppRules.Clear();
        copy.Palette[0].Label = "changed";

        Assert.Single(original.AppRules);
        Assert.Null(original.Palette[0].Label);
    }
}
