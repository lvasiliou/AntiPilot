#include "pch.h"
#include "App.xaml.h"
#include "MainWindow.xaml.h"

using namespace winrt;
using namespace Microsoft::UI::Xaml;

namespace winrt::AntiPilot::Shell::implementation
{
    void App::OnLaunched(LaunchActivatedEventArgs const&)
    {
        _window = make<MainWindow>();
        _window.Activate();
    }
}
