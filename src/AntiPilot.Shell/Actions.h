#pragma once

#include "Config.h"

#include <string>
#include <utility>
#include <vector>
#include <winrt/base.h>

namespace AntiPilot::Shell::Actions
{
    // A short label for lists. Never empty. A port of KeyAction.Describe on the .NET side; it lives
    // here rather than in Config because it needs the string table, which the key path has not got.
    winrt::hstring Describe(KeyAction const& action);

    // What the action points at, for the palette list's third column: the app, the path, the chord.
    winrt::hstring DescribeTarget(KeyAction const& action);

    // Every app with a visible top-level window, as (process name, window title), for the
    // "pick a running app" list. Store apps are reported as themselves, not as the frame host.
    std::vector<std::pair<std::wstring, std::wstring>> ListWindowedApps();
}
