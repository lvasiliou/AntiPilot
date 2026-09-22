#pragma once

#include "App.xaml.g.h"

// The generated XAML type table (XamlTypeInfo.g.cpp) creates every runtimeclass used in markup
// by its implementation type, and only sees the headers the XAML-backed classes include. Str is
// not XAML-backed, so it is declared here for that table's benefit.
#include "Str.h"
#include "SettingsCard.xaml.h"

namespace winrt::AntiPilot::Shell::implementation
{
    struct App : AppT<App>
    {
        App() = default;

        void OnLaunched(Microsoft::UI::Xaml::LaunchActivatedEventArgs const&);

    private:
        Microsoft::UI::Xaml::Window _window{ nullptr };
    };
}
