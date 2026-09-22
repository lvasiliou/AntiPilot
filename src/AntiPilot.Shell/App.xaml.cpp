#include "pch.h"
#include "App.xaml.h"
#include "MainWindow.xaml.h"

#include <winrt/Microsoft.Windows.AppLifecycle.h>

using namespace winrt;
using namespace Microsoft::UI::Xaml;

namespace winrt::AntiPilot::Shell::implementation
{
    void App::OnLaunched(LaunchActivatedEventArgs const&)
    {
        _window = make<MainWindow>();
        _window.Activate();

        // A second start was redirected here (see main.cpp); the answer is to come to the front.
        Microsoft::Windows::AppLifecycle::AppInstance::GetCurrent().Activated([this](auto&&, auto&&)
        {
            _window.DispatcherQueue().TryEnqueue([this] { _window.Activate(); });
        });
    }
}
