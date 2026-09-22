#pragma once

#include "PalettePage.g.h"
#include "ActionEditor.xaml.h"

#include "Config.h"

#include <functional>

namespace winrt::AntiPilot::Shell::implementation
{
    struct PalettePage : PalettePageT<PalettePage>
    {
        PalettePage() = default;

        void Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty);

        fire_and_forget OnAdd(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        fire_and_forget OnEdit(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void OnRemove(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void OnMoveUp(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void OnMoveDown(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);

    private:
        ::AntiPilot::AppConfig* _config = nullptr;
        HWND _owner{};
        std::function<void()> _dirty;

        void Refresh();
        void Move(int delta);

        /// <summary>Add (index -1) or edit one entry through a dialog, applying the result to the config.</summary>
        Windows::Foundation::IAsyncAction EditEntry(int index);
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct PalettePage : PalettePageT<PalettePage, implementation::PalettePage>
    {
    };
}
