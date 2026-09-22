#include "pch.h"
#include "MainWindow.xaml.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "Log.h"
#include "Strings.h"

#include <microsoft.ui.xaml.window.h>
#include <winrt/Microsoft.UI.Windowing.h>

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Controls;
using namespace winrt::Windows::Foundation;

namespace winrt::AntiPilot::Shell::implementation
{
    void MainWindow::InitializeComponent()
    {
        MainWindowT::InitializeComponent();

        Title(::AntiPilot::Shell::Strings::Get(L"SettingsTitle"));
        this->try_as<::IWindowNative>()->get_WindowHandle(&_hwnd);

        _single = make<SinglePressPage>();
        get_self<SinglePressPage>(_single)->Load(_config, _hwnd, [this] { _dirty = true; });
        PageHost().Content(_single);

        // The title-bar close goes through the same "save your changes?" as the Cancel button.
        AppWindow().Closing({ this, &MainWindow::OnClosing });
    }

    void MainWindow::OnNavigationChanged(NavigationView const&, NavigationViewSelectionChangedEventArgs const& e)
    {
        auto item = e.SelectedItem().try_as<NavigationViewItem>();
        auto tag = item ? unbox_value_or<hstring>(item.Tag(), L"") : hstring{};

        // ponytail: only the first page exists yet; the other four show nothing until they are ported.
        PageHost().Content(tag == L"single" ? IInspectable{ _single } : nullptr);
    }

    fire_and_forget MainWindow::OnSave(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();
        if (co_await Save())
        {
            _closing = true;
            Close();
        }
    }

    fire_and_forget MainWindow::OnCancel(IInspectable const&, RoutedEventArgs const&)
    {
        // An explicit Cancel means "throw my edits away", so no question; the title-bar close asks.
        _closing = true;
        Close();
        co_return;
    }

    void MainWindow::OnClosing(Microsoft::UI::Windowing::AppWindow const&, Microsoft::UI::Windowing::AppWindowClosingEventArgs const& e)
    {
        if (_closing || !_dirty)
        {
            return;
        }

        // Cannot await inside a Closing handler, so stop this close and run the question separately.
        e.Cancel(true);
        ConfirmAndClose();
    }

    IAsyncAction MainWindow::ConfirmAndClose()
    {
        auto strong = get_strong();

        if (_dirty)
        {
            ContentDialog dialog;
            dialog.XamlRoot(Content().XamlRoot());
            dialog.Title(box_value(::AntiPilot::Shell::Strings::Get(L"AppName")));
            dialog.Content(box_value(::AntiPilot::Shell::Strings::Get(L"SaveBeforeClosing")));
            dialog.PrimaryButtonText(::AntiPilot::Shell::Strings::Get(L"Save"));
            dialog.SecondaryButtonText(::AntiPilot::Shell::Strings::Get(L"Close"));
            dialog.CloseButtonText(::AntiPilot::Shell::Strings::Get(L"Cancel"));
            dialog.DefaultButton(ContentDialogButton::Primary);

            switch (co_await dialog.ShowAsync())
            {
            case ContentDialogResult::Primary:
                if (!co_await Save())
                {
                    co_return;
                }
                break;

            case ContentDialogResult::Secondary:
                break;

            default:
                co_return;
            }
        }

        _closing = true;
        Close();
    }

    IAsyncOperation<bool> MainWindow::Save()
    {
        auto strong = get_strong();

        // ponytail: no validation pass yet (ActionValidator on the .NET side); it comes with the
        // rules and palette pages, which are where a target most often goes missing.
        std::wstring error = _config.Save();
        if (!error.empty())
        {
            ::AntiPilot::Log::Write(L"Could not save settings: " + error);
            co_await Tell(hstring{ std::wstring{ ::AntiPilot::Shell::Strings::Get(L"CouldNotSave") } + L"\n\n" + error });
            co_return false;
        }

        _dirty = false;
        ::AntiPilot::Log::Write(L"Settings saved.");
        co_return true;
    }

    IAsyncAction MainWindow::Tell(hstring body)
    {
        ContentDialog dialog;
        dialog.XamlRoot(Content().XamlRoot());
        dialog.Title(box_value(::AntiPilot::Shell::Strings::Get(L"AppName")));
        dialog.Content(box_value(body));
        dialog.CloseButtonText(::AntiPilot::Shell::Strings::Get(L"Ok"));
        co_await dialog.ShowAsync();
    }
}
