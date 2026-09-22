#pragma once

#include "DoublePressPage.g.h"
#include "ActionEditor.xaml.h"
#include "SettingsCard.xaml.h"

#include "Config.h"

#include <functional>

namespace winrt::AntiPilot::Shell::implementation
{
    struct DoublePressPage : DoublePressPageT<DoublePressPage>
    {
        DoublePressPage() = default;

        void Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty);

        void OnEnabledToggled(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void OnWindowChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Controls::Primitives::RangeBaseValueChangedEventArgs const&);

    private:
        ::AntiPilot::AppConfig* _config = nullptr;
        std::function<void()> _dirty;
        bool _loading = false;

        void Refresh();
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct DoublePressPage : DoublePressPageT<DoublePressPage, implementation::DoublePressPage>
    {
    };
}
