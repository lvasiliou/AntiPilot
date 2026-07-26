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

    public KeyAction Clone() => (KeyAction)MemberwiseClone();

    [JsonIgnore]
    public bool IsConfigured => Kind switch
    {
        ActionKind.None => false,
        ActionKind.ShellApp => !string.IsNullOrWhiteSpace(Aumid),
        ActionKind.File => !string.IsNullOrWhiteSpace(Path),
        ActionKind.MenuKey => true,
        _ => false,
    };

    public string Describe() => Kind switch
    {
        ActionKind.ShellApp => string.IsNullOrWhiteSpace(DisplayName) ? (Aumid ?? "(no app)") : DisplayName!,
        ActionKind.File => string.IsNullOrWhiteSpace(Path) ? "(nothing)" : System.IO.Path.GetFileName(Path),
        ActionKind.MenuKey => "Menu key",
        _ => "Nothing",
    };
}

public sealed class AppConfig
{
    /// <summary>
    /// What the key does. There is deliberately no separate press-and-hold action: Windows only
    /// reports hold state through URI activation, which it does not use to launch this app.
    /// </summary>
    public KeyAction Tap { get; set; } = new();

    /// <summary>Set once the tray icon has explained where Windows put it.</summary>
    public bool TrayIntroShown { get; set; }

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntiPilot");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg is not null)
                {
                    cfg.Tap ??= new KeyAction();
                    return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to read config: {ex}");
        }

        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        // Write-then-move so a half-written file never survives a crash.
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, ConfigPath, overwrite: true);
    }
}
