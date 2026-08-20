using Xunit;

namespace AntiPilot.Tests;

/// <summary>
/// Issue #1: choosing "Nothing" opened the settings window on every press instead of doing
/// nothing. ActionKind.None was carrying two meanings at once — "never set up" and "set up as
/// Nothing" — and the key path could not tell them apart.
/// </summary>
public class KeyPressOutcomeTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "AntiPilot.Tests", Guid.NewGuid().ToString("N"));

    private string PathFor(string name) => Path.Combine(_directory, name);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Saves and reloads, which is the only way a config legitimately becomes "saved".</summary>
    private AppConfig RoundTrip(AppConfig config)
    {
        var path = PathFor("config.json");
        config.SaveTo(path);
        var loaded = AppConfig.LoadFrom(path);
        Assert.NotNull(loaded);
        return loaded!;
    }

    [Fact]
    public void AFreshInstallOpensSettings()
    {
        // No file on disk: the user has never been to the settings window, and a key that appears
        // to do nothing on a brand new install is how people conclude the app is broken.
        var config = new AppConfig();

        Assert.False(config.HasBeenSaved);
        Assert.Equal(KeyPressOutcome.OpenSettings, config.OutcomeFor(config.Tap));
    }

    [Fact]
    public void ChoosingNothingDoesNothing()
    {
        // The bug: this used to open the settings window on every single press.
        var config = RoundTrip(new AppConfig { Tap = new KeyAction { Kind = ActionKind.None } });

        Assert.True(config.HasBeenSaved);
        Assert.Equal(KeyPressOutcome.DoNothing, config.OutcomeFor(config.Tap));
    }

    [Fact]
    public void SavingWithoutChoosingAnActionAlsoDoesNothing()
    {
        // The path that makes this more than an edge case: open settings on a fresh install, turn
        // on the tray icon, press Save. The action is still None, but it is now a saved None.
        var config = RoundTrip(new AppConfig { TrayIntroShown = true });

        Assert.Equal(KeyPressOutcome.DoNothing, config.OutcomeFor(config.Tap));
    }

    [Theory]
    [InlineData(ActionKind.MenuKey)]
    [InlineData(ActionKind.Palette)]
    public void AConfiguredActionRuns(ActionKind kind)
    {
        var config = RoundTrip(new AppConfig { Tap = new KeyAction { Kind = kind } });
        Assert.Equal(KeyPressOutcome.RunAction, config.OutcomeFor(config.Tap));
    }

    [Fact]
    public void AKindChosenWithoutATargetGoesBackToSettings()
    {
        // "Launch an app" with no app picked. The user meant something and did not finish, so
        // sending them back is right — this is the one case the old behaviour got correct.
        var config = RoundTrip(new AppConfig { Tap = new KeyAction { Kind = ActionKind.ShellApp } });

        Assert.Equal(KeyPressOutcome.OpenSettings, config.OutcomeFor(config.Tap));
    }

    [Fact]
    public void HasBeenSavedIsNotStoredInTheFile()
    {
        // The existence of the file is the signal, so nothing is serialised and there is no
        // migration for anyone upgrading from a version that never wrote it.
        var path = PathFor("config.json");
        new AppConfig().SaveTo(path);

        Assert.DoesNotContain("HasBeenSaved", File.ReadAllText(path));
    }

    [Fact]
    public void SavingMarksAConfigAsSavedWithoutReloading()
    {
        var config = new AppConfig { Tap = new KeyAction { Kind = ActionKind.None } };
        Assert.Equal(KeyPressOutcome.OpenSettings, config.OutcomeFor(config.Tap));

        config.SaveTo(PathFor("config.json"));
        Assert.Equal(KeyPressOutcome.DoNothing, config.OutcomeFor(config.Tap));
    }

    [Fact]
    public void TheRejectedFixWouldHaveBrokenPerAppRules()
    {
        // Guards the reason IsConfigured was left alone. If None counted as configured, a rule
        // with no action would become usable and shadow the single-press action, so the key would
        // go dead in that one app with no explanation.
        var config = new AppConfig
        {
            Tap = new KeyAction { Kind = ActionKind.MenuKey },
            AppRules = [new AppRule { ProcessName = "code", Action = new KeyAction() }],
        };

        Assert.False(config.AppRules[0].IsUsable);
        Assert.Equal(ActionKind.MenuKey, config.ResolveTap("code").Kind);
    }
}
