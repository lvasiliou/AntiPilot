#pragma once

#include <string>
#include <string_view>

namespace AntiPilot::Focus
{
    /// <summary>
    /// Executable name (no extension) of the app in the foreground, or empty when there is none.
    /// Used to pick a per-app rule. Store apps are reported as themselves, not as the frame host
    /// that owns their top-level window.
    /// </summary>
    std::wstring GetForegroundProcessName();

    /// <summary>
    /// Brings the target's existing window to the front, or minimises it when it is already there
    /// and <paramref name="allowMinimise"/> is set. False means nothing matched and the caller
    /// should launch instead. Either of <paramref name="aumid"/> and <paramref name="path"/> may be empty.
    /// </summary>
    bool TryFocus(std::wstring_view aumid, std::wstring_view path, bool allowMinimise);
}
