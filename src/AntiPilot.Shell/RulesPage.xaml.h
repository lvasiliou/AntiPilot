#pragma once

#include "RulesPage.g.h"
#include "ActionEditor.xaml.h"

#include "Config.h"

#include <functional>

namespace winrt::AntiPilot::Shell::implementation
{
    struct RulesPage : RulesPageT<RulesPage>
    {
        RulesPage() = default;

        void Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty);

        fire_and_forget OnAdd(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnEdit(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void OnRemove(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);

    private:
        ::AntiPilot::AppConfig* _config = nullptr;
        HWND _owner{};
        std::function<void()> _dirty;

        void Refresh();

        /// <summary>Add (index -1) or edit one rule through a dialog, applying the result to the config.</summary>
        Windows::Foundation::IAsyncAction EditRule(int index);
        Windows::Foundation::IAsyncAction Tell(hstring body);
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct RulesPage : RulesPageT<RulesPage, implementation::RulesPage>
    {
    };
}
