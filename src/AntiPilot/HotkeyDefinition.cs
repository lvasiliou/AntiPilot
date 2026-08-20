using System.Diagnostics.CodeAnalysis;

namespace AntiPilot;

/// <summary>
/// A keyboard chord, stored in config as text ("Ctrl+Shift+Escape") so the JSON stays readable and
/// hand-editable. Deliberately free of any WinForms reference: the key-press path parses these
/// before any UI assembly is touched.
/// </summary>
public sealed class HotkeyDefinition : IEquatable<HotkeyDefinition>
{
    public HotkeyDefinition(int virtualKey, bool control = false, bool alt = false, bool shift = false, bool windows = false)
    {
        VirtualKey = virtualKey;
        Control = control;
        Alt = alt;
        Shift = shift;
        Windows = windows;
    }

    public int VirtualKey { get; }

    public bool Control { get; }

    public bool Alt { get; }

    public bool Shift { get; }

    public bool Windows { get; }

    /// <summary>
    /// True for keys Windows expects the E0 prefix on. Getting this wrong is not cosmetic: the
    /// arrows and the numeric keypad share virtual-key codes and are told apart by this flag alone.
    /// </summary>
    public bool IsExtended => ExtendedKeys.Contains(VirtualKey);

    private static readonly HashSet<int> ExtendedKeys =
    [
        0x21, 0x22, 0x23, 0x24,             // PageUp, PageDown, End, Home
        0x25, 0x26, 0x27, 0x28,             // Left, Up, Right, Down
        0x2C, 0x2D, 0x2E,                   // PrintScreen, Insert, Delete
        0x5B, 0x5C, 0x5D,                   // LWin, RWin, Apps
        0x6F,                               // Divide (keypad)
        0x90,                               // NumLock
        0xA3, 0xA5,                         // RControl, RMenu
        0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC,   // browser keys
        0xAD, 0xAE, 0xAF,                   // volume mute / down / up
        0xB0, 0xB1, 0xB2, 0xB3,             // media next / previous / stop / play-pause
        0xB4, 0xB5, 0xB6, 0xB7,             // launch mail / media select / app1 / app2
    ];

    /// <summary>
    /// Names accepted when parsing and produced when formatting. First entry per key is canonical;
    /// the rest are aliases so a hand-typed "Esc" or "PgDn" still parses.
    /// </summary>
    private static readonly (int VirtualKey, string[] Names)[] KeyNames =
    [
        (0x08, ["Backspace", "Back"]),
        (0x09, ["Tab"]),
        (0x0D, ["Enter", "Return"]),
        (0x13, ["Pause", "Break"]),
        (0x14, ["CapsLock", "Capital"]),
        (0x1B, ["Escape", "Esc"]),
        (0x20, ["Space", "Spacebar"]),
        (0x21, ["PageUp", "PgUp", "Prior"]),
        (0x22, ["PageDown", "PgDn", "Next"]),
        (0x23, ["End"]),
        (0x24, ["Home"]),
        (0x25, ["Left", "LeftArrow"]),
        (0x26, ["Up", "UpArrow"]),
        (0x27, ["Right", "RightArrow"]),
        (0x28, ["Down", "DownArrow"]),
        (0x2C, ["PrintScreen", "PrtScn", "Snapshot"]),
        (0x2D, ["Insert", "Ins"]),
        (0x2E, ["Delete", "Del"]),
        (0x5D, ["Menu", "Apps", "ContextMenu"]),
        (0x90, ["NumLock"]),
        (0x91, ["ScrollLock", "Scroll"]),
        (0x6A, ["Multiply"]),
        (0x6B, ["Add"]),
        (0x6D, ["Subtract"]),
        (0x6E, ["Decimal"]),
        (0x6F, ["Divide"]),
        (0xAD, ["VolumeMute"]),
        (0xAE, ["VolumeDown"]),
        (0xAF, ["VolumeUp"]),
        (0xB0, ["MediaNext"]),
        (0xB1, ["MediaPrevious"]),
        (0xB2, ["MediaStop"]),
        (0xB3, ["MediaPlayPause"]),
        (0xBA, [";", "Semicolon"]),
        (0xBB, ["=", "Equals"]),
        (0xBC, [",", "Comma"]),
        (0xBD, ["-", "Minus"]),
        (0xBE, [".", "Period"]),
        (0xBF, ["/", "Slash"]),
        (0xC0, ["`", "Backtick"]),
        (0xDB, ["[", "LeftBracket"]),
        (0xDC, ["\\", "Backslash"]),
        (0xDD, ["]", "RightBracket"]),
        (0xDE, ["'", "Quote"]),
    ];

    private static readonly Dictionary<string, int> NameToKey = BuildNameLookup();

    private static Dictionary<string, int> BuildNameLookup()
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (virtualKey, names) in KeyNames)
        {
            foreach (var name in names)
            {
                lookup[name] = virtualKey;
            }
        }

        for (int i = 0; i < 26; i++)
        {
            lookup[((char)('A' + i)).ToString()] = 0x41 + i;
        }

        for (int i = 0; i < 10; i++)
        {
            lookup[i.ToString()] = 0x30 + i;
        }

        for (int i = 1; i <= 24; i++)
        {
            lookup["F" + i] = 0x6F + i;
        }

        for (int i = 0; i < 10; i++)
        {
            lookup["Num" + i] = 0x60 + i;
        }

        return lookup;
    }

    /// <summary>The canonical name of a key on its own, e.g. 0x1B becomes "Escape".</summary>
    public static string NameOf(int virtualKey)
    {
        foreach (var (key, names) in KeyNames)
        {
            if (key == virtualKey)
            {
                return names[0];
            }
        }

        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return "F" + (virtualKey - 0x6F);
        }

        if (virtualKey is >= 0x60 and <= 0x69)
        {
            return "Num" + (virtualKey - 0x60);
        }

        return $"0x{virtualKey:X2}";
    }

    /// <summary>True for the modifier keys themselves, which cannot be the tail of a chord.</summary>
    public static bool IsModifierKey(int virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12 or          // Shift, Control, Menu (Alt)
        0xA0 or 0xA1 or 0xA2 or 0xA3 or  // L/R Shift, L/R Control
        0xA4 or 0xA5 or                  // L/R Menu
        0x5B or 0x5C;                    // L/R Win

    public static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyDefinition? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        bool control = false, alt = false, shift = false, windows = false;
        int? key = null;

        // Split on '+' but keep a trailing "+" key, which would otherwise vanish into separators.
        foreach (var raw in SplitParts(text!))
        {
            var part = raw.Trim();
            if (part.Length == 0)
            {
                continue;
            }

            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control" or "ctl":
                    control = true;
                    continue;
                case "alt" or "menu":
                    alt = true;
                    continue;
                case "shift":
                    shift = true;
                    continue;
                case "win" or "windows" or "meta" or "super" or "cmd":
                    windows = true;
                    continue;
            }

            if (!NameToKey.TryGetValue(part, out int parsed) || key is not null)
            {
                return false;
            }

            key = parsed;
        }

        if (key is null || IsModifierKey(key.Value))
        {
            return false;
        }

        result = new HotkeyDefinition(key.Value, control, alt, shift, windows);
        return true;
    }

    private static IEnumerable<string> SplitParts(string text)
    {
        var parts = text.Split('+');

        for (int i = 0; i < parts.Length; i++)
        {
            // "Ctrl++" means Ctrl plus the "+" key: an empty slot followed by the last one.
            if (parts[i].Length == 0 && i == parts.Length - 2 && parts[^1].Length == 0)
            {
                yield return "Add";
                yield break;
            }

            yield return parts[i];
        }
    }

    /// <summary>Round-trips through <see cref="TryParse"/>.</summary>
    public string Format()
    {
        var parts = new List<string>(5);

        if (Control)
        {
            parts.Add("Ctrl");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Windows)
        {
            parts.Add("Win");
        }

        parts.Add(NameOf(VirtualKey));
        return string.Join("+", parts);
    }

    public override string ToString() => Format();

    public bool Equals(HotkeyDefinition? other) =>
        other is not null &&
        other.VirtualKey == VirtualKey &&
        other.Control == Control &&
        other.Alt == Alt &&
        other.Shift == Shift &&
        other.Windows == Windows;

    public override bool Equals(object? obj) => Equals(obj as HotkeyDefinition);

    public override int GetHashCode() => HashCode.Combine(VirtualKey, Control, Alt, Shift, Windows);
}
