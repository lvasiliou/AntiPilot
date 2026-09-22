#pragma once

#include <string_view>
#include <winrt/base.h>

namespace AntiPilot::Shell::Strings
{
    // One user-visible string by its key in tools\strings\en.txt, in the package's language.
    // Never throws: a key that is not there comes back as !Key!, the way the .NET accessor does,
    // so a typo shows on screen instead of taking the window down.
    winrt::hstring Get(std::wstring_view key);

    // A string that carries a {0} placeholder, with the value in it. A translation with a broken
    // placeholder shows the raw text rather than taking the window down.
    winrt::hstring Format(std::wstring_view key, int value);
    winrt::hstring Format(std::wstring_view key, std::wstring_view value);
}
