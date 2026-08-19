using Xunit;

namespace AntiPilot.Tests;

public class AppRuleTests
{
    [Theory]
    [InlineData("chrome", "chrome", true)]
    [InlineData("chrome.exe", "chrome", true)]
    [InlineData("chrome", "chrome.exe", true)]
    [InlineData("CHROME.EXE", "chrome", true)]
    [InlineData("  chrome  ", "chrome", true)]
    [InlineData("chrome", "firefox", false)]
    [InlineData("chrome", null, false)]
    [InlineData("chrome", "", false)]
    public void MatchesRegardlessOfHowTheNameWasTyped(string ruleName, string? foreground, bool expected)
    {
        var rule = new AppRule { ProcessName = ruleName };
        Assert.Equal(expected, rule.Matches(foreground));
    }

    [Fact]
    public void AnEmptyRuleMatchesNothing()
    {
        Assert.False(new AppRule().Matches("chrome"));
        Assert.False(new AppRule { ProcessName = "   " }.Matches("chrome"));
    }

    [Fact]
    public void IsUsableNeedsBothHalves()
    {
        Assert.False(new AppRule { ProcessName = "chrome" }.IsUsable);
        Assert.False(new AppRule { Action = new KeyAction { Kind = ActionKind.MenuKey } }.IsUsable);

        Assert.True(new AppRule
        {
            ProcessName = "chrome",
            Action = new KeyAction { Kind = ActionKind.MenuKey },
        }.IsUsable);
    }

    [Fact]
    public void ResolveTapPrefersTheFirstMatchingRule()
    {
        var config = new AppConfig
        {
            Tap = new KeyAction { Kind = ActionKind.MenuKey },
            AppRules =
            [
                new AppRule { ProcessName = "code", Action = new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "Ctrl+P" } },
                new AppRule { ProcessName = "code", Action = new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "Ctrl+B" } },
            ],
        };

        Assert.Equal("Ctrl+P", config.ResolveTap("code").Hotkey);
    }

    [Fact]
    public void ResolveTapFallsThroughToTheSinglePressAction()
    {
        var config = new AppConfig
        {
            Tap = new KeyAction { Kind = ActionKind.MenuKey },
            AppRules =
            [
                new AppRule { ProcessName = "code", Action = new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "Ctrl+P" } },
            ],
        };

        Assert.Equal(ActionKind.MenuKey, config.ResolveTap("notepad").Kind);
        Assert.Equal(ActionKind.MenuKey, config.ResolveTap(null).Kind);
    }

    [Fact]
    public void AnUnusableRuleIsSkippedRatherThanApplied()
    {
        // A rule pointing at nothing must not shadow the single-press action, or the key would go
        // dead in that one app with no explanation.
        var config = new AppConfig
        {
            Tap = new KeyAction { Kind = ActionKind.MenuKey },
            AppRules = [new AppRule { ProcessName = "code", Action = new KeyAction() }],
        };

        Assert.Equal(ActionKind.MenuKey, config.ResolveTap("code").Kind);
    }
}
