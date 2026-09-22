#pragma once

#include <string>
#include <vector>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Imaging.h>

namespace AntiPilot::Shell::Apps
{
    // One entry of the shell's Apps folder: everything the Start menu's "All apps" list shows.
    struct Entry
    {
        std::wstring name;
        std::wstring parsingName; // an AUMID for packaged apps
    };

    // Everything in shell:AppsFolder except AntiPilot's own entries, which pointing the key at
    // would only start AntiPilot. Slow (hundreds of shell items); call off the UI thread.
    std::vector<Entry> Enumerate();

    // True when the shell can still resolve the entry, which is how "the app was uninstalled" is detected.
    bool Exists(std::wstring_view parsingName);

    // The entry's icon at about the given size, or nullptr. Slow for the same reason; call off the UI thread.
    winrt::Windows::Graphics::Imaging::SoftwareBitmap TryGetIcon(std::wstring_view parsingName, int size);
}
