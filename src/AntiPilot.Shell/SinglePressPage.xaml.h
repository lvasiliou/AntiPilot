#pragma once

#include "SinglePressPage.g.h"
#include "ActionEditor.xaml.h"

#include "Config.h"

#include <functional>

namespace winrt::AntiPilot::Shell::implementation
{
    struct SinglePressPage : SinglePressPageT<SinglePressPage>
    {
        SinglePressPage() = default;

        /// <summary>
        /// Shows the single-press action and writes every edit straight back into
        /// <paramref name="config"/>, telling <paramref name="onDirty"/> each time.
        /// </summary>
        void Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty);
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct SinglePressPage : SinglePressPageT<SinglePressPage, implementation::SinglePressPage>
    {
    };
}
