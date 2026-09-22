#include "pch.h"
#include "MainWindow.xaml.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "Focus.h"
#include "KeyStatus.h"
#include "Launch.h"
#include "Log.h"
#include "Strings.h"
#include "Text.h"
#include "Validate.h"

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
        TitleText().Text(Title());
        this->try_as<::IWindowNative>()->get_WindowHandle(&_hwnd);

        // Our own title bar: the backdrop runs under the caption buttons and the grid at the top
        // of the markup is what the window is dragged by.
        ExtendsContentIntoTitleBar(true);
        SetTitleBar(TitleBar());

        // Pages are built on first visit, not up front: the first page is what the user waits for,
        // and the other four together cost as much again to construct.
        PageHost().Content(PageFor(L"single"));

        // The status card invites a trip to Windows Settings, so it has to be right when the user
        // comes back; reading it once at start-up leaves it stale exactly when it matters.
        RefreshStatus();
        Activated([this](auto&&, WindowActivatedEventArgs const& e)
        {
            if (e.WindowActivationState() != WindowActivationState::Deactivated)
            {
                RefreshStatus();
            }
        });

        // The title-bar close goes through "save your changes?"; the Cancel button does not.
        AppWindow().Closing({ this, &MainWindow::OnClosing });
    }

    IInspectable MainWindow::PageFor(hstring const& tag)
    {
        auto dirty = [this] { _dirty = true; };

        if (tag == L"single")
        {
            if (!_single) { _single = make<SinglePressPage>(); get_self<SinglePressPage>(_single)->Load(_config, _hwnd, dirty); }
            return _single;
        }
        if (tag == L"double")
        {
            if (!_double) { _double = make<DoublePressPage>(); get_self<DoublePressPage>(_double)->Load(_config, _hwnd, dirty); }
            return _double;
        }
        if (tag == L"rules")
        {
            if (!_rules) { _rules = make<RulesPage>(); get_self<RulesPage>(_rules)->Load(_config, _hwnd, dirty); }
            return _rules;
        }
        if (tag == L"palette")
        {
            if (!_palette) { _palette = make<PalettePage>(); get_self<PalettePage>(_palette)->Load(_config, _hwnd, dirty); }
            return _palette;
        }
        if (tag == L"general")
        {
            if (!_general)
            {
                _general = make<GeneralPage>();
                get_self<GeneralPage>(_general)->Load(_config, _hwnd, dirty, [this](::AntiPilot::AppConfig imported) { OnImported(std::move(imported)); });
            }
            return _general;
        }

        return nullptr;
    }

    /// <summary>Re-reads the config into every page that exists; an import replaced it underneath them.</summary>
    void MainWindow::LoadPages()
    {
        auto dirty = [this] { _dirty = true; };
        if (_single) get_self<SinglePressPage>(_single)->Load(_config, _hwnd, dirty);
        if (_double) get_self<DoublePressPage>(_double)->Load(_config, _hwnd, dirty);
        if (_rules) get_self<RulesPage>(_rules)->Load(_config, _hwnd, dirty);
        if (_palette) get_self<PalettePage>(_palette)->Load(_config, _hwnd, dirty);
        if (_general) get_self<GeneralPage>(_general)->Load(_config, _hwnd, dirty, [this](::AntiPilot::AppConfig imported) { OnImported(std::move(imported)); });
    }

    void MainWindow::RefreshStatus()
    {
        StatusCard().Title(::AntiPilot::Shell::KeyStatus::Describe());
    }

    void MainWindow::OnImported(::AntiPilot::AppConfig imported)
    {
        // The tray introduction is about this machine, not the settings, so it does not travel.
        imported.trayIntroShown = _config.trayIntroShown;
        _config = std::move(imported);
        _dirty = true;
        LoadPages();
    }

    void MainWindow::OnNavigationChanged(NavigationView const&, NavigationViewSelectionChangedEventArgs const& e)
    {
        auto item = e.SelectedItem().try_as<NavigationViewItem>();
        auto tag = item ? unbox_value_or<hstring>(item.Tag(), L"") : hstring{};
        PageHost().Content(PageFor(tag));
    }

    void MainWindow::OnOpenWindowsSettings(IInspectable const&, RoutedEventArgs const&)
    {
        ::AntiPilot::Shell::KeyStatus::OpenWindowsSettings();
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

    fire_and_forget MainWindow::OnTest(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();
        const ::AntiPilot::KeyAction action = _config.tap;

        if (!action.IsConfigured())
        {
            co_await Tell(::AntiPilot::Shell::Strings::Get(L"NothingConfiguredYet"));
            co_return;
        }

        // The same sequence the key path runs, except that synthesised input would land on this
        // window, so those two kinds get an explanation instead of a press.
        std::wstring failure;
        switch (action.kind)
        {
        case ::AntiPilot::ActionKind::MenuKey:
            co_await Tell(::AntiPilot::Shell::Strings::Get(L"MenuKeyTestHint"));
            co_return;

        case ::AntiPilot::ActionKind::Hotkey:
            co_await Tell(::AntiPilot::Shell::Strings::Get(L"HotkeyTestHint"));
            co_return;

        case ::AntiPilot::ActionKind::Palette:
            ::AntiPilot::Launch::Delegate({ L"--palette" });
            co_return;

        case ::AntiPilot::ActionKind::ShellApp:
            if (action.behaviour != ::AntiPilot::LaunchBehaviour::Always &&
                ::AntiPilot::Focus::TryFocus(action.aumid, {}, action.behaviour == ::AntiPilot::LaunchBehaviour::Toggle))
            {
                co_return;
            }
            failure = ::AntiPilot::Launch::AppsFolderItem(action.aumid);
            break;

        case ::AntiPilot::ActionKind::File:
        {
            std::wstring fileName = ::AntiPilot::Text::ExpandEnvironment(action.path);
            if (action.behaviour != ::AntiPilot::LaunchBehaviour::Always &&
                ::AntiPilot::Text::EndsWithIgnoreCase(fileName, L".exe") &&
                ::AntiPilot::Focus::TryFocus({}, fileName, action.behaviour == ::AntiPilot::LaunchBehaviour::Toggle))
            {
                co_return;
            }
            failure = ::AntiPilot::Launch::File(action);
            break;
        }

        default:
            co_return;
        }

        if (!failure.empty())
        {
            ::AntiPilot::Log::Write(L"Test failed: " + failure);
            co_await Tell(hstring{ std::wstring{ ::AntiPilot::Shell::Strings::Get(L"ActionFailedTitle") } + L"\n\n" + failure });
        }
    }

    /// <summary>
    /// Warns about actions that point at something no longer there. Advisory, not a veto: a target
    /// can be a removable drive or an app about to be reinstalled, and refusing the save outright
    /// would be wrong in both cases.
    /// </summary>
    IAsyncOperation<bool> MainWindow::ConfirmValidation()
    {
        auto strong = get_strong();
        auto queue = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
        const ::AntiPilot::AppConfig snapshot = _config;

        co_await resume_background();
        auto problems = ::AntiPilot::Shell::Validate::Config(snapshot);
        co_await wil::resume_foreground(queue);

        if (problems.empty())
        {
            co_return true;
        }

        std::wstring message;
        for (auto const& problem : problems)
        {
            message += std::wstring{ problem } + L"\n";
        }

        ContentDialog dialog;
        dialog.XamlRoot(Content().XamlRoot());
        dialog.Title(box_value(::AntiPilot::Shell::Strings::Get(L"ValidationTitle")));
        dialog.Content(box_value(hstring{ message }));
        dialog.PrimaryButtonText(::AntiPilot::Shell::Strings::Get(L"ValidationContinue"));
        dialog.CloseButtonText(::AntiPilot::Shell::Strings::Get(L"Cancel"));
        dialog.DefaultButton(ContentDialogButton::Close);
        co_return co_await dialog.ShowAsync() == ContentDialogResult::Primary;
    }

    IAsyncOperation<bool> MainWindow::Save()
    {
        auto strong = get_strong();

        if (!co_await ConfirmValidation())
        {
            co_return false;
        }

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
