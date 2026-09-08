#include "Hotkey.h"

#include "Text.h"

#include <format>
#include <vector>

namespace AntiPilot
{
    namespace
    {
        constexpr int ExtendedKeys[] =
        {
            0x21, 0x22, 0x23, 0x24,                     // PageUp, PageDown, End, Home
            0x25, 0x26, 0x27, 0x28,                     // Left, Up, Right, Down
            0x2C, 0x2D, 0x2E,                           // PrintScreen, Insert, Delete
            0x5B, 0x5C, 0x5D,                           // LWin, RWin, Apps
            0x6F,                                       // Divide (keypad)
            0x90,                                       // NumLock
            0xA3, 0xA5,                                 // RControl, RMenu
            0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC,   // browser keys
            0xAD, 0xAE, 0xAF,                           // volume mute / down / up
            0xB0, 0xB1, 0xB2, 0xB3,                     // media next / previous / stop / play-pause
            0xB4, 0xB5, 0xB6, 0xB7,                     // launch mail / media select / app1 / app2
        };

        struct NamedKey
        {
            int virtualKey;
            const wchar_t* names[3];
        };

        /// <summary>First entry per key is canonical; the rest are aliases so a hand-typed "Esc" or "PgDn" still parses.</summary>
        constexpr NamedKey KeyNames[] =
        {
            { 0x08, { L"Backspace", L"Back" } },
            { 0x09, { L"Tab" } },
            { 0x0D, { L"Enter", L"Return" } },
            { 0x13, { L"Pause", L"Break" } },
            { 0x14, { L"CapsLock", L"Capital" } },
            { 0x1B, { L"Escape", L"Esc" } },
            { 0x20, { L"Space", L"Spacebar" } },
            { 0x21, { L"PageUp", L"PgUp", L"Prior" } },
            { 0x22, { L"PageDown", L"PgDn", L"Next" } },
            { 0x23, { L"End" } },
            { 0x24, { L"Home" } },
            { 0x25, { L"Left", L"LeftArrow" } },
            { 0x26, { L"Up", L"UpArrow" } },
            { 0x27, { L"Right", L"RightArrow" } },
            { 0x28, { L"Down", L"DownArrow" } },
            { 0x2C, { L"PrintScreen", L"PrtScn", L"Snapshot" } },
            { 0x2D, { L"Insert", L"Ins" } },
            { 0x2E, { L"Delete", L"Del" } },
            { 0x5D, { L"Menu", L"Apps", L"ContextMenu" } },
            { 0x90, { L"NumLock" } },
            { 0x91, { L"ScrollLock", L"Scroll" } },
            { 0x6A, { L"Multiply" } },
            { 0x6B, { L"Add" } },
            { 0x6D, { L"Subtract" } },
            { 0x6E, { L"Decimal" } },
            { 0x6F, { L"Divide" } },
            { 0xAD, { L"VolumeMute" } },
            { 0xAE, { L"VolumeDown" } },
            { 0xAF, { L"VolumeUp" } },
            { 0xB0, { L"MediaNext" } },
            { 0xB1, { L"MediaPrevious" } },
            { 0xB2, { L"MediaStop" } },
            { 0xB3, { L"MediaPlayPause" } },
            { 0xBA, { L";", L"Semicolon" } },
            { 0xBB, { L"=", L"Equals" } },
            { 0xBC, { L",", L"Comma" } },
            { 0xBD, { L"-", L"Minus" } },
            { 0xBE, { L".", L"Period" } },
            { 0xBF, { L"/", L"Slash" } },
            { 0xC0, { L"`", L"Backtick" } },
            { 0xDB, { L"[", L"LeftBracket" } },
            { 0xDC, { L"\\", L"Backslash" } },
            { 0xDD, { L"]", L"RightBracket" } },
            { 0xDE, { L"'", L"Quote" } },
        };

        /// <summary>The virtual key a name stands for, or nothing. Letters, digits, F-keys and Num0-9 are generated rather than listed.</summary>
        std::optional<int> KeyFromName(std::wstring_view name)
        {
            for (const auto& entry : KeyNames)
            {
                for (const wchar_t* alias : entry.names)
                {
                    if (alias != nullptr && Text::EqualsIgnoreCase(name, alias))
                    {
                        return entry.virtualKey;
                    }
                }
            }

            if (name.size() == 1)
            {
                wchar_t c = name[0];
                if (c >= L'a' && c <= L'z') { return 0x41 + (c - L'a'); }
                if (c >= L'A' && c <= L'Z') { return 0x41 + (c - L'A'); }
                if (c >= L'0' && c <= L'9') { return 0x30 + (c - L'0'); }
            }

            auto number = [](std::wstring_view digits) -> std::optional<int>
            {
                if (digits.empty() || digits.size() > 2)
                {
                    return std::nullopt;
                }

                int value = 0;
                for (wchar_t c : digits)
                {
                    if (c < L'0' || c > L'9')
                    {
                        return std::nullopt;
                    }

                    value = value * 10 + (c - L'0');
                }

                return value;
            };

            if (name.size() >= 2 && (name[0] == L'F' || name[0] == L'f'))
            {
                if (auto n = number(name.substr(1)); n && *n >= 1 && *n <= 24)
                {
                    return 0x6F + *n;
                }
            }

            if (name.size() == 4 && Text::StartsWithIgnoreCase(name, L"Num"))
            {
                if (auto n = number(name.substr(3)); n && *n >= 0 && *n <= 9)
                {
                    return 0x60 + *n;
                }
            }

            return std::nullopt;
        }

        /// <summary>
        /// Splits on '+' but keeps a trailing "+" key, which would otherwise vanish into the
        /// separators: "Ctrl++" is Ctrl plus the "+" key.
        /// </summary>
        std::vector<std::wstring> SplitParts(std::wstring_view text)
        {
            std::vector<std::wstring> parts;
            size_t start = 0;
            while (true)
            {
                size_t plus = text.find(L'+', start);
                if (plus == std::wstring_view::npos)
                {
                    parts.emplace_back(text.substr(start));
                    break;
                }

                parts.emplace_back(text.substr(start, plus - start));
                start = plus + 1;
            }

            size_t n = parts.size();
            if (n >= 2 && parts[n - 1].empty() && parts[n - 2].empty())
            {
                parts.resize(n - 2);
                parts.emplace_back(L"Add");
            }

            return parts;
        }
    }

    bool HotkeyDefinition::IsExtended() const
    {
        for (int key : ExtendedKeys)
        {
            if (key == virtualKey)
            {
                return true;
            }
        }

        return false;
    }

    bool HotkeyDefinition::IsModifierKey(int key)
    {
        switch (key)
        {
        case 0x10: case 0x11: case 0x12:                // Shift, Control, Menu (Alt)
        case 0xA0: case 0xA1: case 0xA2: case 0xA3:     // L/R Shift, L/R Control
        case 0xA4: case 0xA5:                           // L/R Menu
        case 0x5B: case 0x5C:                           // L/R Win
            return true;
        default:
            return false;
        }
    }

    std::wstring HotkeyDefinition::NameOf(int key)
    {
        for (const auto& entry : KeyNames)
        {
            if (entry.virtualKey == key)
            {
                return entry.names[0];
            }
        }

        if (key >= 0x41 && key <= 0x5A) { return std::wstring(1, static_cast<wchar_t>(key)); }
        if (key >= 0x30 && key <= 0x39) { return std::wstring(1, static_cast<wchar_t>(key)); }
        if (key >= 0x70 && key <= 0x87) { return L"F" + std::to_wstring(key - 0x6F); }
        if (key >= 0x60 && key <= 0x69) { return L"Num" + std::to_wstring(key - 0x60); }

        return std::format(L"0x{:02X}", key);
    }

    std::optional<HotkeyDefinition> HotkeyDefinition::TryParse(std::wstring_view text)
    {
        if (Text::IsBlank(text))
        {
            return std::nullopt;
        }

        HotkeyDefinition result;
        std::optional<int> key;

        for (const std::wstring& raw : SplitParts(text))
        {
            std::wstring part = Text::Trim(raw);
            if (part.empty())
            {
                continue;
            }

            std::wstring lower = Text::ToLowerInvariant(part);
            if (lower == L"ctrl" || lower == L"control" || lower == L"ctl") { result.control = true; continue; }
            if (lower == L"alt" || lower == L"menu") { result.alt = true; continue; }
            if (lower == L"shift") { result.shift = true; continue; }
            if (lower == L"win" || lower == L"windows" || lower == L"meta" || lower == L"super" || lower == L"cmd") { result.windows = true; continue; }

            auto parsed = KeyFromName(part);
            if (!parsed || key)
            {
                return std::nullopt;
            }

            key = parsed;
        }

        if (!key || IsModifierKey(*key))
        {
            return std::nullopt;
        }

        result.virtualKey = *key;
        return result;
    }

    std::wstring HotkeyDefinition::Format() const
    {
        std::wstring text;
        if (control) { text += L"Ctrl+"; }
        if (alt) { text += L"Alt+"; }
        if (shift) { text += L"Shift+"; }
        if (windows) { text += L"Win+"; }
        return text + NameOf(virtualKey);
    }
}
