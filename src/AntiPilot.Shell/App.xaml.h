#pragma once

#include "App.xaml.g.h"

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
