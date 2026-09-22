#include "pch.h"
#include "ActionEditor.xaml.h"
#if __has_include("ActionEditor.g.cpp")
#include "ActionEditor.g.cpp"
#endif

#include "AppPicker.h"
#include "Hotkey.h"
#include "Strings.h"
#include "Text.h"

#include <winrt/Microsoft.UI.Input.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.Storage.Pickers.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Core.h>
#include <shobjidl_core.h>

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Controls;
using namespace ::AntiPilot;

namespace
{
    // The same list the .NET editor offers, in the same order; the chord text is what the config stores.
    constexpr std::pair<std::wstring_view, std::wstring_view> Presets[] = {
        { L"PresetTaskManager", L"Ctrl+Shift+Escape" },
        { L"PresetClipboard", L"Win+V" },
        { L"PresetSnip", L"Win+Shift+S" },
        { L"PresetExplorer", L"Win+E" },
        { L"PresetEmoji", L"Win+." },
        { L"PresetPrintScreen", L"PrintScreen" },
        { L"PresetLock", L"Win+L" },
        { L"PresetShowDesktop", L"Win+D" },
        { L"PresetPlayPause", L"MediaPlayPause" },
        { L"PresetMute", L"VolumeMute" },
    };

    bool IsDown(Windows::System::VirtualKey key)
    {
        using Windows::UI::Core::CoreVirtualKeyStates;
        auto state = Microsoft::UI::Input::InputKeyboardSource::GetKeyStateForCurrentThread(key);
        return (state & CoreVirtualKeyStates::Down) == CoreVirtualKeyStates::Down;
    }

    hstring Join(std::wstring_view first, std::wstring_view second)
    {
        return hstring{ std::wstring{ first } + L" " + std::wstring{ second } };
    }
}

namespace winrt::AntiPilot::Shell::implementation
{
    void ActionEditor::InitializeComponent()
    {
        ActionEditorT::InitializeComponent();

        _modes = { ActionKind::None, ActionKind::ShellApp, ActionKind::File, ActionKind::MenuKey, ActionKind::Hotkey, ActionKind::Palette };

        AppHint().Text(Join(::AntiPilot::Shell::Strings::Get(L"AppHint"), ::AntiPilot::Shell::Strings::Get(L"BehaviourHint")));
        HotkeyHint().Text(Join(::AntiPilot::Shell::Strings::Get(L"HotkeyHint"), ::AntiPilot::Shell::Strings::Get(L"ElevatedHint")));

        // An empty first entry, so picking a preset is an action rather than a state.
        Presets().Items().Append(box_value(L""));
        for (auto const& [key, chord] : ::Presets)
        {
            Presets().Items().Append(box_value(hstring{ std::wstring{ ::AntiPilot::Shell::Strings::Get(key) } + L"  (" + std::wstring{ chord } + L")" }));
        }
        Presets().SelectedIndex(0);

        Action(KeyAction{});
    }

    void ActionEditor::AllowPalette(bool allow)
    {
        if (!allow && _modes.back() == ActionKind::Palette)
        {
            _modes.pop_back();
            Mode().Items().RemoveAtEnd();
        }
    }

    void ActionEditor::Action(KeyAction const& action)
    {
        _loading = true;
        _action = action;

        auto found = std::find(_modes.begin(), _modes.end(), _action.kind);
        Mode().SelectedIndex(found == _modes.end() ? 0 : static_cast<int>(found - _modes.begin()));

        ShowApp();
        PathBox().Text(hstring{ _action.path });
        ArgsBox().Text(hstring{ _action.arguments });
        WorkDirBox().Text(hstring{ _action.workingDirectory });
        SyncBehaviourCombos();
        ShowHotkey();
        ShowPanelForKind();
        _loading = false;
    }

    void ActionEditor::ShowApp()
    {
        AppName().Text(Text::IsBlank(_action.displayName) ? ::AntiPilot::Shell::Strings::Get(L"NoAppChosen") : hstring{ _action.displayName });
        AppAumid().Text(hstring{ _action.aumid });
        AppIcon().Source(nullptr);
        AppIcon().Visibility(Visibility::Collapsed);
        LoadIcon();
    }

    fire_and_forget ActionEditor::LoadIcon()
    {
        auto strong = get_strong();
        std::wstring aumid = _action.aumid;
        if (Text::IsBlank(aumid))
        {
            co_return;
        }

        // The size depends on the display scale, which is only known once the editor is in the
        // tree. A page loads its editor before it is shown, so come back when it is.
        auto root = XamlRoot();
        if (!root)
        {
            auto token = std::make_shared<event_token>();
            *token = Loaded([weak = get_weak(), token](auto&&, auto&&)
            {
                if (auto self = weak.get())
                {
                    self->Loaded(*token);
                    self->LoadIcon();
                }
            });
            co_return;
        }

        auto source = co_await ::AntiPilot::Shell::AppPicker::IconFor(aumid, static_cast<int>(48 * root.RasterizationScale()));

        // The action may have moved on while the shell was drawing; only the current one gets the icon.
        if (source && _action.aumid == aumid)
        {
            AppIcon().Source(source);
            AppIcon().Visibility(Visibility::Visible);
        }
    }

    fire_and_forget ActionEditor::OnChooseApp(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();
        auto picked = std::make_shared<::AntiPilot::Shell::Apps::Entry>();
        if (co_await ::AntiPilot::Shell::AppPicker::Show(XamlRoot(), _action.aumid, picked))
        {
            _action.aumid = picked->parsingName;
            _action.displayName = picked->name;
            ShowApp();
            Changed();
        }
    }

    void ActionEditor::Changed()
    {
        if (!_loading && _changed)
        {
            _changed();
        }
    }

    void ActionEditor::ShowPanelForKind()
    {
        auto show = [](auto const& element, bool on) { element.Visibility(on ? Visibility::Visible : Visibility::Collapsed); };
        show(NothingPanel(), _action.kind == ActionKind::None);
        show(AppPanel(), _action.kind == ActionKind::ShellApp);
        show(FilePanel(), _action.kind == ActionKind::File);
        show(MenuPanel(), _action.kind == ActionKind::MenuKey);
        show(HotkeyPanel(), _action.kind == ActionKind::Hotkey);
        show(PalettePanel(), _action.kind == ActionKind::Palette);
    }

    void ActionEditor::SyncBehaviourCombos()
    {
        _syncingBehaviour = true;
        AppBehaviour().SelectedIndex(static_cast<int>(_action.behaviour));
        FileBehaviour().SelectedIndex(static_cast<int>(_action.behaviour));
        _syncingBehaviour = false;
    }

    void ActionEditor::ShowHotkey()
    {
        auto parsed = HotkeyDefinition::TryParse(_action.hotkey);
        HotkeyBox().Text(parsed ? hstring{ parsed->Format() } : hstring{});
        WinKey().IsChecked(parsed && parsed->windows);
    }

    void ActionEditor::SetHotkey(std::optional<HotkeyDefinition> const& hotkey)
    {
        _action.hotkey = hotkey ? hotkey->Format() : std::wstring{};
        ShowHotkey();
        Changed();
    }

    // ---- handlers --------------------------------------------------------------

    void ActionEditor::OnModeChanged(IInspectable const&, SelectionChangedEventArgs const&)
    {
        int index = Mode().SelectedIndex();
        if (index < 0 || index >= static_cast<int>(_modes.size()))
        {
            return;
        }

        _action.kind = _modes[static_cast<size_t>(index)];
        ShowPanelForKind();
        Changed();
    }

    void ActionEditor::OnBehaviourChanged(IInspectable const& sender, SelectionChangedEventArgs const&)
    {
        if (_syncingBehaviour)
        {
            return;
        }

        int index = sender.as<ComboBox>().SelectedIndex();
        _action.behaviour = static_cast<LaunchBehaviour>(std::max(0, index));
        SyncBehaviourCombos();
        Changed();
    }

    void ActionEditor::OnPathChanged(IInspectable const&, TextChangedEventArgs const&)
    {
        _action.path = PathBox().Text();
        Changed();
    }

    void ActionEditor::OnArgsChanged(IInspectable const&, TextChangedEventArgs const&)
    {
        _action.arguments = ArgsBox().Text();
        Changed();
    }

    void ActionEditor::OnWorkDirChanged(IInspectable const&, TextChangedEventArgs const&)
    {
        _action.workingDirectory = WorkDirBox().Text();
        Changed();
    }

    fire_and_forget ActionEditor::OnBrowse(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();

        Windows::Storage::Pickers::FileOpenPicker picker;
        picker.as<IInitializeWithWindow>()->Initialize(_owner);
        for (auto extension : { L".exe", L".lnk", L".bat", L".cmd", L".ps1", L".url", L"*" })
        {
            picker.FileTypeFilter().Append(extension);
        }

        auto file = co_await picker.PickSingleFileAsync();
        if (file)
        {
            PathBox().Text(file.Path());
        }
    }

    void ActionEditor::OnHotkeyKeyDown(IInspectable const&, Input::KeyRoutedEventArgs const& e)
    {
        using Windows::System::VirtualKey;

        int key = static_cast<int>(e.Key());
        if (e.Key() == VirtualKey::Escape || key == 0 || HotkeyDefinition::IsModifierKey(key))
        {
            return;
        }

        HotkeyDefinition chord;
        chord.virtualKey = key;
        chord.control = IsDown(VirtualKey::Control);
        chord.alt = IsDown(VirtualKey::Menu);
        chord.shift = IsDown(VirtualKey::Shift);
        chord.windows = WinKey().IsChecked().GetBoolean();

        e.Handled(true);
        SetHotkey(chord);
    }

    void ActionEditor::OnWinChanged(IInspectable const&, RoutedEventArgs const&)
    {
        if (_loading)
        {
            return;
        }

        if (auto parsed = HotkeyDefinition::TryParse(_action.hotkey))
        {
            parsed->windows = WinKey().IsChecked().GetBoolean();
            SetHotkey(parsed);
        }
    }

    void ActionEditor::OnHotkeyClear(IInspectable const&, RoutedEventArgs const&)
    {
        SetHotkey(std::nullopt);
    }

    void ActionEditor::OnPresetChanged(IInspectable const&, SelectionChangedEventArgs const&)
    {
        int index = Presets().SelectedIndex() - 1;
        if (_loading || index < 0 || index >= static_cast<int>(std::size(::Presets)))
        {
            return;
        }

        if (auto parsed = HotkeyDefinition::TryParse(::Presets[index].second))
        {
            SetHotkey(parsed);
        }
    }
}
