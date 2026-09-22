#pragma once

#include "MainWindow.g.h"

namespace winrt::AntiPilot::Shell::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow() = default;
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
