#pragma once

#include "Hotkey.h"

namespace AntiPilot::Input
{
    /// <summary>Synthesises a press of the Menu / context-menu key (VK_APPS) into whatever has focus.</summary>
    void SendMenuKey();

    /// <summary>
    /// Synthesises a chord: modifiers down, key down, key up, modifiers up. Order matters — a
    /// window that reads the modifier state on key-down sees nothing if the modifiers arrive late.
    /// </summary>
    void SendHotkey(const HotkeyDefinition& hotkey);

    /// <summary>
    /// The physical Copilot key is a Shift+Win+F23 chord under the hood. If any of those are still
    /// logically down when we run, whatever we do next gets mangled, so drop them first.
    /// </summary>
    void ReleaseStuckModifiers();
}
