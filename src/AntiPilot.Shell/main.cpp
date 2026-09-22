#include "pch.h"
#include "App.xaml.h"

#include <winrt/Microsoft.Windows.AppLifecycle.h>

using namespace winrt;
using namespace winrt::Microsoft::Windows::AppLifecycle;

// The XAML compiler's generated wWinMain is disabled (DISABLE_XAML_GENERATED_MAIN) so the window
// can be single-instance: a second start of the settings window hands its activation to the one
// already open, which brings itself forward, rather than opening a second copy of the same file.
int __stdcall wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    init_apartment(apartment_type::single_threaded);

    auto instance = AppInstance::FindOrRegisterForKey(L"settings");
    if (!instance.IsCurrent())
    {
        // Blocking on an async call is not allowed on this thread, so wait the way the App SDK
        // documents: an event the completion sets, pumped by CoWaitForMultipleObjects.
        HANDLE done = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        auto redirect = instance.RedirectActivationToAsync(AppInstance::GetCurrent().GetActivatedEventArgs());
        redirect.Completed([done](auto&&, auto&&) { SetEvent(done); });

        DWORD index = 0;
        CoWaitForMultipleObjects(CWMO_DEFAULT, INFINITE, 1, &done, &index);
        CloseHandle(done);
        return 0;
    }

    Microsoft::UI::Xaml::Application::Start([](auto&&)
    {
        make<AntiPilot::Shell::implementation::App>();
    });

    return 0;
}
