#pragma once

#include <string_view>
#include <winrt/base.h>

namespace AntiPilot::Shell::Strings
{
    // One user-visible string by its key in tools\strings\en.txt, in the package's language.
    // Never throws: a key that is not there comes back as !Key!, the way the .NET accessor does,
    // so a typo shows on screen instead of taking the window down.
    winrt::hstring Get(std::wstring_view key);
}
