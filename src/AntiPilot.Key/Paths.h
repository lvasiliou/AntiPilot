#pragma once

#include <string>
#include <string_view>

namespace AntiPilot::Paths
{
    /// <summary>%LOCALAPPDATA%\AntiPilot — the same folder the settings window writes to.</summary>
    std::wstring ConfigDirectory();

    std::wstring ConfigPath();

    /// <summary>The folder this executable was loaded from, which inside the package is the package root.</summary>
    std::wstring OwnDirectory();

    /// <summary>A file that lives next to this executable: the .NET half of the app, for the parts that need a window.</summary>
    std::wstring Sibling(std::wstring_view fileName);

    /// <summary>Creates the folder and every missing parent. True when it exists afterwards.</summary>
    bool EnsureDirectory(const std::wstring& path);
}
