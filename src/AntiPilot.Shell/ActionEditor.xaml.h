#pragma once

#include "ActionEditor.g.h"

#include "Config.h"
#include "Hotkey.h"

#include <functional>
#include <optional>
#include <vector>

namespace winrt::AntiPilot::Shell::implementation
{
    struct ActionEditor : ActionEditorT<ActionEditor>
    {
        ActionEditor() = default;

        void InitializeComponent();

        // ---- the C++ side, for the page that hosts this --------------------------

        /// <summary>The window, for the file picker, which needs one to appear over.</summary>
        void Owner(HWND window) { _owner = window; }

        /// <summary>Hides the palette option: a palette entry that opened the palette would loop.</summary>
        void AllowPalette(bool allow);

        /// <summary>What "Nothing" says on this particular editor.</summary>
        void NothingHint(hstring const& text) { NothingPanel().Text(text); }

        ::AntiPilot::KeyAction const& Action() const { return _action; }
        void Action(::AntiPilot::KeyAction const& action);

        /// <summary>Called on every edit the user makes, not on loading.</summary>
        void OnChanged(std::function<void()> handler) { _changed = std::move(handler); }

        // ---- handlers wired in the markup ----------------------------------------

        void OnModeChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Controls::SelectionChangedEventArgs const&);
        void OnBehaviourChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Controls::SelectionChangedEventArgs const&);
        void OnPathChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Controls::TextChangedEventArgs const&);
        void OnArgsChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Controls::TextChangedEventArgs const&);
        void OnWorkDirChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Controls::TextChangedEventArgs const&);
        fire_and_forget OnBrowse(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void OnHotkeyKeyDown(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Input::KeyRoutedEventArgs const&);
        void OnWinChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void OnHotkeyClear(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void OnPresetChanged(Windows::Foundation::IInspectable const&, Microsoft::UI::Xaml::Controls::SelectionChangedEventArgs const&);

    private:
        ::AntiPilot::KeyAction _action;
        std::vector<::AntiPilot::ActionKind> _modes;
        std::function<void()> _changed;
        HWND _owner{};
        bool _loading = false;
        bool _syncingBehaviour = false;

        void Changed();
        void ShowPanelForKind();
        void ShowHotkey();
        void SetHotkey(std::optional<::AntiPilot::HotkeyDefinition> const& hotkey);
        void SyncBehaviourCombos();
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct ActionEditor : ActionEditorT<ActionEditor, implementation::ActionEditor>
    {
    };
}
