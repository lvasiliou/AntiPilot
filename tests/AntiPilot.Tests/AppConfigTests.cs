using Xunit;

namespace AntiPilot.Tests;

public class AppConfigTests : IDisposable
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
            // A leftover temp directory is not worth failing a test run over.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RoundTripsEverythingItStores()
    {
        var original = new AppConfig
        {
            Tap = new KeyAction { Kind = ActionKind.ShellApp, Aumid = "Some.App_abc!App", DisplayName = "Some App", Behaviour = LaunchBehaviour.Toggle },
            DoubleTap = new KeyAction { Kind = ActionKind.Hotkey, Hotkey = "Ctrl+Shift+Escape" },
            DoubleTapEnabled = true,
            DoubleTapWindowMs = 400,
            Language = "es",
            AppRules =
            [
                new AppRule { ProcessName = "chrome", Action = new KeyAction { Kind = ActionKind.MenuKey } },
            ],
            Palette =
            [
                new KeyAction { Kind = ActionKind.File, Path = @"C:\Windows\notepad.exe", Label = "Notes" },
            ],
        };

        var path = PathFor("config.json");
        original.SaveTo(path);

        var loaded = AppConfig.LoadFrom(path);

        Assert.NotNull(loaded);
        Assert.Equal(ActionKind.ShellApp, loaded!.Tap.Kind);
        Assert.Equal("Some.App_abc!App", loaded.Tap.Aumid);
        Assert.Equal(LaunchBehaviour.Toggle, loaded.Tap.Behaviour);
        Assert.True(loaded.DoubleTapEnabled);
        Assert.Equal(400, loaded.DoubleTapWindowMs);
        Assert.Equal("Ctrl+Shift+Escape", loaded.DoubleTap.Hotkey);
        Assert.Equal("es", loaded.Language);
        Assert.Single(loaded.AppRules);
        Assert.Equal("chrome", loaded.AppRules[0].ProcessName);
        Assert.Single(loaded.Palette);
        Assert.Equal("Notes", loaded.Palette[0].Label);
        Assert.Equal(AppConfig.CurrentSchema, loaded.Schema);
    }

    [Fact]
    public void MissingFileIsNotAnError()
    {
        Assert.Null(AppConfig.LoadFrom(PathFor("nothing-here.json")));
    }

    [Fact]
    public void GarbageIsRejectedRatherThanThrown()
    {
        var path = PathFor("broken.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ this is not json");

        Assert.Null(AppConfig.LoadFrom(path));
    }

    [Fact]
    public void FillsInWhatAnOlderFileLeftOut()
    {
        var path = PathFor("v1.json");
        Directory.CreateDirectory(_directory);

        // Schema 1: no double press, no rules, no palette.
        File.WriteAllText(path, """
            {
              "Tap": { "Kind": "MenuKey" }
            }
            """);

        var loaded = AppConfig.LoadFrom(path);

        Assert.NotNull(loaded);
        Assert.Equal(ActionKind.MenuKey, loaded!.Tap.Kind);
        Assert.NotNull(loaded.DoubleTap);
        Assert.False(loaded.DoubleTapEnabled);
        Assert.Empty(loaded.AppRules);
        Assert.Empty(loaded.Palette);
    }

    [Theory]
    [InlineData(0, AppConfig.MinDoubleTapWindowMs)]
    [InlineData(50, AppConfig.MinDoubleTapWindowMs)]
    [InlineData(5000, AppConfig.MaxDoubleTapWindowMs)]
    [InlineData(400, 400)]
    public void ClampsAHandEditedDoublePressWindow(int stored, int expected)
    {
        // The window is a delay on every single press, so a hand-edited 5000 would make the key
        // look broken. Normalise is the only thing standing between that and the user.
        var config = new AppConfig { DoubleTapWindowMs = stored };
        config.Normalise();

        Assert.Equal(expected, config.DoubleTapWindowMs);
    }

    [Fact]
    public void WriteIsAtomicEnoughToLeaveNoTempFileBehind()
    {
        var path = PathFor("config.json");
        new AppConfig().SaveTo(path);

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
