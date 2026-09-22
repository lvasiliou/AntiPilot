#pragma once

#include "Config.h"

#include <optional>
#include <string>
#include <vector>
#include <winrt/base.h>

namespace AntiPilot::Shell::Validate
{
    // What is wrong with an action, in the user's words, or nothing when it looks fine. A port of
    // ActionValidator.cs: a key that quietly does nothing is the worst failure this app has, and
    // the usual cause is mundane, the app uninstalled or the file moved. Slow for ShellApp (asks
    // the shell); call off the UI thread.
    std::optional<winrt::hstring> Action(KeyAction const& action);

    // Every problem in the config, each prefixed with where it is: "Single press: ...".
    std::vector<winrt::hstring> Config(AppConfig const& config);
}
