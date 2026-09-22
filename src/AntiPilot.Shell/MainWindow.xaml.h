#pragma once

#include "MainWindow.g.h"
#include "SinglePressPage.xaml.h"

#include "Config.h"

namespace winrt::AntiPilot::Shell::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow() = default;

        void InitializeComponent();

        void OnNavigationChanged(Microsoft::UI::Xaml::Controls::NavigationView const&, Microsoft::UI::Xaml::Controls::NavigationViewSelectionChangedEventArgs const&);
        fire_and_forget OnSave(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnCancel(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);

    private:
        // The window owns the one config every page edits in place. Save writes it; Cancel drops it.
        ::AntiPilot::AppConfig _config = ::AntiPilot::AppConfig::Load();
        bool _dirty = false;
        bool _closing = false;
        HWND _hwnd{};

        Shell::SinglePressPage _single{ nullptr };

        void OnClosing(Microsoft::UI::Windowing::AppWindow const&, Microsoft::UI::Windowing::AppWindowClosingEventArgs const&);
        Windows::Foundation::IAsyncAction ConfirmAndClose();
        Windows::Foundation::IAsyncOperation<bool> Save();
        Windows::Foundation::IAsyncAction Tell(hstring body);
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
