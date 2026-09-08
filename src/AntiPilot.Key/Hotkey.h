#pragma once

#include <optional>
#include <string>
#include <string_view>

namespace AntiPilot
{
    /// <summary>
    /// A keyboard chord, stored in config as text ("Ctrl+Shift+Escape") so the JSON stays readable
    /// and hand-editable. The names and aliases here are the same list the .NET side parses and
    /// formats with, so a chord written by either half reads back in the other.
    /// </summary>
    struct HotkeyDefinition
    {
        int virtualKey = 0;
        bool control = false;
        bool alt = false;
        bool shift = false;
        bool windows = false;

        /// <summary>
        /// True for keys Windows expects the E0 prefix on. Getting this wrong is not cosmetic: the
        /// arrows and the numeric keypad share virtual-key codes and are told apart by this flag alone.
        /// </summary>
        bool IsExtended() const;

        /// <summary>Round-trips through <see cref="TryParse"/>: "Ctrl+Alt+Shift+Win+Key" in that order.</summary>
        std::wstring Format() const;

        static std::optional<HotkeyDefinition> TryParse(std::wstring_view text);

        /// <summary>The canonical name of a key on its own, e.g. 0x1B becomes "Escape".</summary>
        static std::wstring NameOf(int virtualKey);

        /// <summary>True for the modifier keys themselves, which cannot be the tail of a chord.</summary>
        static bool IsModifierKey(int virtualKey);

        bool operator==(const HotkeyDefinition&) const = default;
    };
}
