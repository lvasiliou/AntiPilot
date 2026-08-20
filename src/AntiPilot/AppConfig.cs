using System.Text.Json;
using System.Text.Json.Serialization;

namespace AntiPilot;

public enum ActionKind
{
    /// <summary>Do nothing at all.</summary>
    None = 0,

    /// <summary>Launch an entry from the Start menu / Apps folder (Store apps included), by AUMID.</summary>
    ShellApp = 1,

    /// <summary>Launch an arbitrary file, shortcut, folder or URL through ShellExecute.</summary>
    File = 2,

    /// <summary>Behave like the old Menu / context-menu key (VK_APPS).</summary>
    MenuKey = 3,

    /// <summary>Synthesise any keyboard chord into whatever has focus.</summary>
    Hotkey = 4,

    /// <summary>Show the quick-launch palette.</summary>
    Palette = 5,
}

/// <summary>What to do when the target of a launch action already has a window open.</summary>
public enum LaunchBehaviour
{
    /// <summary>Hand the target to the shell every time, however many copies that leaves running.</summary>
    Always = 0,

    /// <summary>Bring an existing window to the front instead of starting a second copy.</summary>
    FocusIfRunning = 1,

    /// <summary>Focus it, or minimise it when it is already the foreground window.</summary>
    Toggle = 2,
}

public sealed class KeyAction
{
    public ActionKind Kind { get; set; } = ActionKind.None;

    /// <summary>Parsing name of the Apps-folder item; an AUMID for packaged apps.</summary>
    public string? Aumid { get; set; }

    /// <summary>Friendly name, kept only so the UI can show what was picked.</summary>
    public string? DisplayName { get; set; }

    public string? Path { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }

    /// <summary>A chord in the form <see cref="HotkeyDefinition"/> round-trips, e.g. "Ctrl+Shift+Escape".</summary>
    public string? Hotkey { get; set; }

    /// <summary>Only consulted for <see cref="ActionKind.ShellApp"/> and <see cref="ActionKind.File"/>.</summary>
    public LaunchBehaviour Behaviour { get; set; } = LaunchBehaviour.Always;

    /// <summary>Overrides <see cref="Describe"/> where the user names their own entries, i.e. the palette.</summary>
    public string? Label { get; set; }

    public KeyAction Clone() => (KeyAction)MemberwiseClone();

    [JsonIgnore]
    public bool IsConfigured => Kind switch
    {
        ActionKind.None => false,
        ActionKind.ShellApp => !string.IsNullOrWhiteSpace(Aumid),
        ActionKind.File => !string.IsNullOrWhiteSpace(Path),
        ActionKind.MenuKey => true,
        ActionKind.Hotkey => HotkeyDefinition.TryParse(Hotkey, out _),
        ActionKind.Palette => true,
        _ => false,
    };

    /// <summary>A short label for menus, tooltips and lists. Never empty.</summary>
    public string Describe()
    {
        if (!string.IsNullOrWhiteSpace(Label))
        {
            return Label!;
        }

        return Kind switch
        {
            ActionKind.ShellApp => string.IsNullOrWhiteSpace(DisplayName) ? (Aumid ?? Strings.NoApp) : DisplayName!,
            ActionKind.File => string.IsNullOrWhiteSpace(Path) ? Strings.Nothing : System.IO.Path.GetFileName(Path!),
            ActionKind.MenuKey => Strings.MenuKeyShort,
            ActionKind.Hotkey => string.IsNullOrWhiteSpace(Hotkey) ? Strings.Nothing : Hotkey!,
            ActionKind.Palette => Strings.PaletteShort,
            _ => Strings.Nothing,
        };
    }
}

/// <summary>One "while this app is in front, do that instead" override.</summary>
public sealed class AppRule
{
    /// <summary>Process executable name, with or without the extension. Matched case-insensitively.</summary>
    public string? ProcessName { get; set; }

    public KeyAction Action { get; set; } = new();

    [JsonIgnore]
    public bool IsUsable => !string.IsNullOrWhiteSpace(ProcessName) && Action.IsConfigured;

    /// <summary>True when this rule is the one to apply to <paramref name="foregroundProcess"/>.</summary>
    public bool Matches(string? foregroundProcess)
    {
        if (string.IsNullOrWhiteSpace(ProcessName) || string.IsNullOrWhiteSpace(foregroundProcess))
        {
            return false;
        }

        return Normalise(ProcessName!).Equals(Normalise(foregroundProcess!), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Strips the extension so "chrome" and "chrome.exe" are the same rule.</summary>
    public static string Normalise(string value)
    {
        value = value.Trim();
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
    }

    public AppRule Clone() => new() { ProcessName = ProcessName, Action = Action.Clone() };
}

/// <summary>What a press of the key should actually do, once the configuration has had its say.</summary>
public enum KeyPressOutcome
{
    /// <summary>Nothing has ever been set up, so show the user where to set it up.</summary>
    OpenSettings,

    /// <summary>The user chose "Nothing". Honour that and stay out of the way.</summary>
    DoNothing,

    /// <summary>Carry out the configured action.</summary>
    RunAction,
}

public sealed class AppConfig
{
    /// <summary>Bumped when the shape changes, so an imported file can be checked before it is trusted.</summary>
    public const int CurrentSchema = 2;

    /// <summary>Below this a second press cannot be hit reliably; above it every press feels broken.</summary>
    public const int MinDoubleTapWindowMs = 200;

    public const int MaxDoubleTapWindowMs = 1000;

    public int Schema { get; set; } = CurrentSchema;

    /// <summary>
    /// What a single press does. Windows only reports press-and-hold through URI activation, which
    /// it does not use to launch this app, so a hold cannot be told from a tap — see
    /// <see cref="TapCoordinator"/> for the one distinction that is detectable.
    /// </summary>
    public KeyAction Tap { get; set; } = new();

    /// <summary>What two quick presses do. Ignored unless <see cref="DoubleTapEnabled"/> is set.</summary>
    public KeyAction DoubleTap { get; set; } = new();

    /// <summary>
    /// Off by default and deliberately so: noticing a second press means holding the first one back
    /// for <see cref="DoubleTapWindowMs"/>, and every single press then pays that delay.
    /// </summary>
    public bool DoubleTapEnabled { get; set; }

    /// <summary>How long to wait for a second press, in milliseconds.</summary>
    public int DoubleTapWindowMs { get; set; } = 350;

    /// <summary>Foreground-app overrides, tried in order before <see cref="Tap"/>.</summary>
    public List<AppRule> AppRules { get; set; } = [];

    /// <summary>Entries offered by the quick-launch palette.</summary>
    public List<KeyAction> Palette { get; set; } = [];

    /// <summary>Set once the tray icon has explained where Windows put it.</summary>
    public bool TrayIntroShown { get; set; }

    /// <summary>UI language as a BCP-47 tag. Null or empty follows Windows.</summary>
    public string? Language { get; set; }

    /// <summary>
    /// True when these settings came from a file the user has actually saved.
    ///
    /// This is what separates "never set up" from "set up as Nothing", which
    /// <see cref="ActionKind.None"/> alone cannot: it is both the value a fresh install starts
    /// with and the value a user picks to switch the key off. Not serialised — the existence of
    /// the file is the signal, so there is nothing to store and nothing to migrate.
    /// </summary>
    [JsonIgnore]
    public bool HasBeenSaved { get; private set; }

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntiPilot");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load() => LoadFrom(ConfigPath) ?? new AppConfig();

    /// <summary>Reads a config file. Returns null when it is missing, unreadable or not one of ours.</summary>
    public static AppConfig? LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions);
            if (config is not null)
            {
                config.Normalise();
                config.HasBeenSaved = true;
            }

            return config;
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to read config from '{path}': {ex}");
            return null;
        }
    }

    /// <summary>Fills in whatever an older or hand-edited file left out, and clamps what it got wrong.</summary>
    public void Normalise()
    {
        Tap ??= new KeyAction();
        DoubleTap ??= new KeyAction();
        AppRules ??= [];
        Palette ??= [];
        AppRules.RemoveAll(rule => rule is null);
        Palette.RemoveAll(action => action is null);

        foreach (var rule in AppRules)
        {
            rule.Action ??= new KeyAction();
        }

        DoubleTapWindowMs = Math.Clamp(DoubleTapWindowMs, MinDoubleTapWindowMs, MaxDoubleTapWindowMs);
        Schema = CurrentSchema;
    }

    /// <summary>The action a press should run, once foreground-app rules have had their say.</summary>
    public KeyAction ResolveTap(string? foregroundProcess)
    {
        foreach (var rule in AppRules)
        {
            if (rule.IsUsable && rule.Matches(foregroundProcess))
            {
                Log.Write($"App rule for '{rule.ProcessName}' matched.");
                return rule.Action;
            }
        }

        return Tap;
    }

    public void Save() => SaveTo(ConfigPath);

    public void SaveTo(string path)
    {
        Schema = CurrentSchema;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);

        // Write-then-move so a half-written file never survives a crash.
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);

        HasBeenSaved = true;
    }

    /// <summary>
    /// Decides what a press should do.
    ///
    /// Reported as issue #1: choosing "Nothing" opened the settings window on every press, because
    /// the key path asked <see cref="KeyAction.IsConfigured"/>, which is false for
    /// <see cref="ActionKind.None"/>. The obvious fix — make None count as configured — was
    /// rejected: IsConfigured means "this action will do something" and is read in ten other
    /// places, where the current answer is the right one. A rule with no action would start
    /// shadowing the single-press action, the tray would offer "Run: Nothing" as a command, and
    /// the double-press delay would arm itself for an action that does nothing.
    ///
    /// The real problem is that one value carried two meanings. Splitting them here leaves
    /// IsConfigured alone and keeps first-run onboarding, which is the one case where opening the
    /// settings window is genuinely helpful.
    /// </summary>
    public KeyPressOutcome OutcomeFor(KeyAction action)
    {
        if (!HasBeenSaved)
        {
            return KeyPressOutcome.OpenSettings;
        }

        if (action.Kind == ActionKind.None)
        {
            return KeyPressOutcome.DoNothing;
        }

        // A kind was chosen but its target never was — "launch an app" with no app. The user meant
        // something, so send them back to finish it.
        return action.IsConfigured ? KeyPressOutcome.RunAction : KeyPressOutcome.OpenSettings;
    }

    public AppConfig Clone()
    {
        var copy = (AppConfig)MemberwiseClone();
        copy.Tap = Tap.Clone();
        copy.DoubleTap = DoubleTap.Clone();
        copy.AppRules = AppRules.Select(rule => rule.Clone()).ToList();
        copy.Palette = Palette.Select(action => action.Clone()).ToList();
        return copy;
    }
}
