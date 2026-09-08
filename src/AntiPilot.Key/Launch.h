#pragma once

#include "Config.h"

#include <initializer_list>
#include <string>
#include <string_view>

namespace AntiPilot::Launch
{
    /// <summary>
    /// Launches an entry of the Apps folder. Packaged apps (AUMIDs, which contain '!') go through
    /// the activation manager; everything else is handed to the shell as a "shell:AppsFolder\..." path.
    /// </summary>
    /// <returns>Empty on success, otherwise what went wrong in words.</returns>
    std::wstring AppsFolderItem(std::wstring_view parsingName);

    /// <summary>Hands a File action to the shell: a program, a document, a folder or a URL, with its arguments and start-in folder.</summary>
    std::wstring File(const KeyAction& action);

    /// <summary>
    /// Starts the .NET half of the app with the given arguments and returns without waiting. This
    /// is how the parts that need a window — the palette, the settings window, a failure balloon —
    /// are reached from a process that deliberately has none.
    /// </summary>
    bool Delegate(std::initializer_list<std::wstring_view> arguments);
}
