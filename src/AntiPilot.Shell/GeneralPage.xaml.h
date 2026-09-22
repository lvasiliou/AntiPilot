#pragma once

#include "GeneralPage.g.h"
#include "SettingsCard.xaml.h"

#include "Config.h"

#include <functional>

namespace winrt::AntiPilot::Shell::implementation
{
    struct GeneralPage : GeneralPageT<GeneralPage>
    {
        GeneralPage() = default;

        void Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty,
                  std::function<void(::AntiPilot::AppConfig)> onImport);

        fire_and_forget OnStartupToggled(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnLanguageChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Controls::SelectionChangedEventArgs const&);
        fire_and_forget OnExport(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnImport(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnOpenLog(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnAbout(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);

    private:
        ::AntiPilot::AppConfig* _config = nullptr;
        HWND _owner{};
        std::function<void()> _dirty;
        std::function<void(::AntiPilot::AppConfig)> _import;
        bool _loading = false;

        void RefreshStartup();
        void ShowLanguage();
        Windows::Foundation::IAsyncAction Tell(hstring body);
        Windows::Foundation::IAsyncOperation<bool> Ask(hstring body);
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct GeneralPage : GeneralPageT<GeneralPage, implementation::GeneralPage>
    {
    };
}
