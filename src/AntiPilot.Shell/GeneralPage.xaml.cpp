#include "pch.h"
#include "GeneralPage.xaml.h"
#if __has_include("GeneralPage.g.cpp")
#include "GeneralPage.g.cpp"
#endif

#include "Log.h"
#include "Paths.h"
#include "Startup.h"
#include "Strings.h"
#include "Text.h"

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Globalization.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.Storage.Pickers.h>
#include <winrt/Windows.System.h>
#include <shobjidl_core.h>

#include <format>

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Controls;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Storage;
using namespace ::AntiPilot::Shell;

namespace
{
    // The languages with a translation, tags only; the names come from Windows so each one is
    // written the way its own speakers write it. Same list and order as the WinForms window.
    constexpr const wchar_t* LanguageTags[] = { L"en", L"ru", L"es", L"zh-Hans", L"pt-BR", L"tr", L"ja", L"ko", L"ar", L"id", L"zh-Hant", L"el", L"uk" };

    template <typename T>
    void Init(T const& picker, HWND owner)
    {
        picker.template as<IInitializeWithWindow>()->Initialize(owner);
    }
}

namespace winrt::AntiPilot::Shell::implementation
{
    void GeneralPage::Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty,
                           std::function<void(::AntiPilot::AppConfig)> onImport)
    {
        _config = &config;
        _owner = owner;
        _dirty = std::move(onDirty);
        _import = std::move(onImport);

        if (Languages().Items().Size() == 0)
        {
            Languages().Items().Append(box_value(Strings::Get(L"LanguageSystem")));
            for (auto tag : LanguageTags)
            {
                Languages().Items().Append(box_value(Windows::Globalization::Language{ tag }.NativeName()));
            }
        }

        ShowLanguage();
        RefreshStartup();
    }

    void GeneralPage::ShowLanguage()
    {
        _loading = true;
        int index = 0;
        for (int i = 0; i < static_cast<int>(std::size(LanguageTags)); i++)
        {
            if (::AntiPilot::Text::EqualsIgnoreCase(_config->language, LanguageTags[i]))
            {
                index = i + 1;
            }
        }

        Languages().SelectedIndex(index);
        _loading = false;
    }

    void GeneralPage::RefreshStartup()
    {
        auto state = Startup::GetState();

        _loading = true;
        Startup().IsOn(state == Startup::Availability::On);
        _loading = false;

        switch (state)
        {
        case Startup::Availability::Unavailable:
            Startup().IsEnabled(false);
            StartupCard().Description(Strings::Get(L"TrayNeedsInstall"));
            break;

        case Startup::Availability::BlockedByUser:
            Startup().IsEnabled(true);
            StartupCard().Description(Strings::Get(L"TrayBlocked"));
            break;

        default:
            Startup().IsEnabled(true);
            StartupCard().Description(Strings::Get(L"TrayDescription"));
            break;
        }
    }

    fire_and_forget GeneralPage::OnStartupToggled(IInspectable const&, RoutedEventArgs const&)
    {
        if (_loading || !_config)
        {
            co_return;
        }

        auto strong = get_strong();

        if (Startup().IsOn())
        {
            auto result = Startup::Enable();
            if (result == Startup::Availability::BlockedByUser)
            {
                RefreshStartup();
                co_await Tell(Strings::Get(L"TrayStartupBlockedBody"));
                co_return;
            }

            if (result != Startup::Availability::On)
            {
                RefreshStartup();
                co_await Tell(Strings::Get(L"TrayStartupFailed"));
                co_return;
            }

            // Flipping the switch should produce an icon now, not only after the next sign-in.
            if (!Startup::StartTray())
            {
                co_await Tell(Strings::Get(L"TrayCouldNotStart"));
            }
        }
        else
        {
            // And switching it off should take the icon away now, not only at the next sign-in.
            Startup::Disable();
            Startup::StopTray();
            RefreshStartup();
        }
    }

    fire_and_forget GeneralPage::OnLanguageChanged(IInspectable const&, SelectionChangedEventArgs const&)
    {
        if (_loading || !_config)
        {
            co_return;
        }

        auto strong = get_strong();
        int index = Languages().SelectedIndex();
        _config->language = index <= 0 ? std::wstring{} : std::wstring{ LanguageTags[index - 1] };
        _dirty();

        // Relabelling every control already on screen is more machinery than the change is worth,
        // and the window is about to be saved and closed anyway.
        co_await Tell(Strings::Get(L"LanguageRestartHint"));
    }

    fire_and_forget GeneralPage::OnExport(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();

        Pickers::FileSavePicker picker;
        Init(picker, _owner);
        picker.SuggestedFileName(L"antipilot-settings");
        picker.FileTypeChoices().Insert(L"JSON", single_threaded_vector<hstring>({ L".json" }));

        auto file = co_await picker.PickSaveFileAsync();
        if (!file)
        {
            co_return;
        }

        std::wstring error = _config->SaveTo(std::wstring{ file.Path() });
        if (!error.empty())
        {
            co_await Tell(hstring{ std::wstring{ Strings::Get(L"CouldNotSave") } + L"\n\n" + error });
            co_return;
        }

        ::AntiPilot::Log::Write(L"Settings exported to '" + std::wstring{ file.Path() } + L"'.");
        co_await Tell(Strings::Get(L"ExportDone"));
    }

    fire_and_forget GeneralPage::OnImport(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();

        Pickers::FileOpenPicker picker;
        Init(picker, _owner);
        picker.FileTypeFilter().Append(L".json");

        auto file = co_await picker.PickSingleFileAsync();
        if (!file)
        {
            co_return;
        }

        auto imported = ::AntiPilot::AppConfig::LoadFrom(std::wstring{ file.Path() });
        if (!imported)
        {
            co_await Tell(Strings::Get(L"ImportFailed"));
            co_return;
        }

        if (!co_await Ask(Strings::Get(L"ImportConfirm")))
        {
            co_return;
        }

        ::AntiPilot::Log::Write(L"Settings imported from '" + std::wstring{ file.Path() } + L"'.");
        _import(std::move(*imported));
        co_await Tell(Strings::Get(L"ImportDone"));
    }

    fire_and_forget GeneralPage::OnOpenLog(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();
        ::AntiPilot::Log::Write(L"Log opened from settings.");

        hstring failure;
        try
        {
            auto file = co_await StorageFile::GetFileFromPathAsync(hstring{ ::AntiPilot::Log::Path() });
            co_await Windows::System::Launcher::LaunchFileAsync(file);
        }
        catch (hresult_error const& e)
        {
            failure = e.message();
        }

        if (!failure.empty())
        {
            co_await Tell(failure);
        }
    }

    fire_and_forget GeneralPage::OnAbout(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();

        hstring version = Strings::Get(L"AboutVersionUnknown");
        hstring identity = Strings::Get(L"AboutUnpackaged");
        try
        {
            auto package = Windows::ApplicationModel::Package::Current();
            auto v = package.Id().Version();
            version = Strings::Format(L"AboutVersion", std::format(L"{}.{}.{}", v.Major, v.Minor, v.Build));
            identity = package.Id().FamilyName();
        }
        catch (hresult_error const&)
        {
        }

        hstring licence;
        try
        {
            auto file = co_await StorageFile::GetFileFromPathAsync(hstring{ ::AntiPilot::Paths::Sibling(L"LICENSE") });
            licence = co_await FileIO::ReadTextAsync(file);
        }
        catch (hresult_error const& e)
        {
            ::AntiPilot::Log::Write(L"About: could not read LICENSE: " + std::wstring{ e.message() });
        }

        StackPanel body;
        body.Spacing(4);
        auto add = [&](hstring const& text, hstring const& style)
        {
            TextBlock block;
            block.Text(text);
            block.TextWrapping(TextWrapping::Wrap);
            if (!style.empty())
            {
                block.Style(Application::Current().Resources().Lookup(box_value(style)).as<Microsoft::UI::Xaml::Style>());
            }
            body.Children().Append(block);
        };
        add(Strings::Get(L"AppName"), L"SubtitleTextBlockStyle");
        add(version, L"");
        add(identity, L"CaptionTextBlockStyle");
        add(Strings::Get(L"AboutTagline"), L"");

        if (!licence.empty())
        {
            TextBlock text;
            text.Text(licence);
            text.TextWrapping(TextWrapping::Wrap);
            text.Style(Application::Current().Resources().Lookup(box_value(L"CaptionTextBlockStyle")).as<Microsoft::UI::Xaml::Style>());
            ScrollViewer scroller;
            scroller.MaxHeight(220);
            scroller.Margin(ThicknessHelper::FromLengths(0, 12, 0, 0));
            scroller.Content(text);
            body.Children().Append(scroller);
        }

        ContentDialog dialog;
        dialog.XamlRoot(XamlRoot());
        dialog.Title(box_value(Strings::Get(L"AboutTitle")));
        dialog.Content(body);
        dialog.CloseButtonText(Strings::Get(L"Close"));
        co_await dialog.ShowAsync();
    }

    IAsyncAction GeneralPage::Tell(hstring body)
    {
        ContentDialog dialog;
        dialog.XamlRoot(XamlRoot());
        dialog.Title(box_value(Strings::Get(L"AppName")));
        dialog.Content(box_value(body));
        dialog.CloseButtonText(Strings::Get(L"Ok"));
        co_await dialog.ShowAsync();
    }

    IAsyncOperation<bool> GeneralPage::Ask(hstring body)
    {
        ContentDialog dialog;
        dialog.XamlRoot(XamlRoot());
        dialog.Title(box_value(Strings::Get(L"AppName")));
        dialog.Content(box_value(body));
        dialog.PrimaryButtonText(Strings::Get(L"Ok"));
        dialog.CloseButtonText(Strings::Get(L"Cancel"));
        dialog.DefaultButton(ContentDialogButton::Primary);
        co_return co_await dialog.ShowAsync() == ContentDialogResult::Primary;
    }
}
