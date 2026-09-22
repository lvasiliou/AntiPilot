#pragma once

#include <winrt/base.h>

namespace AntiPilot::Shell::KeyStatus
{
    // What Windows currently does with the Copilot key, in the user's words: pointed at this app,
    // at Search, at another app, or never changed. A port of CopilotKeyStatus.cs.
    winrt::hstring Describe();

    // The "Customize Copilot key on keyboard" setting. Tries the deep link first and falls back
    // to the page it sits on.
    winrt::fire_and_forget OpenWindowsSettings();
}
