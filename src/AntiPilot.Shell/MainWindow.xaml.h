#pragma once

#include "MainWindow.g.h"
#include "SinglePressPage.xaml.h"
#include "DoublePressPage.xaml.h"
#include "RulesPage.xaml.h"
#include "PalettePage.xaml.h"
#include "GeneralPage.xaml.h"

#include "Config.h"

namespace winrt::AntiPilot::Shell::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow() = default;

        void InitializeComponent();

        void OnNavigationChanged(Microsoft::UI::Xaml::Controls::NavigationView const&, Microsoft::UI::Xaml::Controls::NavigationViewSelectionChangedEventArgs const&);
        void OnOpenWindowsSettings(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnSave(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnCancel(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnTest(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);

    private:
        // The window owns the one config every page edits in place. Save writes it; Cancel drops it.
        ::AntiPilot::AppConfig _config = ::AntiPilot::AppConfig::Load();
        bool _dirty = false;
        bool _closing = false;
        HWND _hwnd{};

        Shell::SinglePressPage _single{ nullptr };
        Shell::DoublePressPage _double{ nullptr };
        Shell::RulesPage _rules{ nullptr };
        Shell::PalettePage _palette{ nullptr };
        Shell::GeneralPage _general{ nullptr };

        /// <summary>The page for a navigation tag, created and loaded the first time it is asked for.</summary>
        Windows::Foundation::IInspectable PageFor(hstring const& tag);
        void LoadPages();
        void RefreshStatus();
        void OnImported(::AntiPilot::AppConfig imported);
        void OnClosing(Microsoft::UI::Windowing::AppWindow const&, Microsoft::UI::Windowing::AppWindowClosingEventArgs const&);
        Windows::Foundation::IAsyncAction ConfirmAndClose();
        Windows::Foundation::IAsyncOperation<bool> ConfirmValidation();
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
