#include "pch.h"
#include "DoublePressPage.xaml.h"
#if __has_include("DoublePressPage.g.cpp")
#include "DoublePressPage.g.cpp"
#endif

#include "Strings.h"

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;

namespace winrt::AntiPilot::Shell::implementation
{
    void DoublePressPage::Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty)
    {
        _config = &config;
        _dirty = std::move(onDirty);

        _loading = true;
        Enabled().IsOn(config.doubleTapEnabled);
        Window().Value(config.doubleTapWindowMs);
        _loading = false;

        auto editor = get_self<ActionEditor>(Editor());
        editor->Owner(owner);
        editor->NothingHint(::AntiPilot::Shell::Strings::Get(L"NothingHintDouble"));
        editor->Action(config.doubleTap);
        editor->OnChanged([this, editor]
        {
            _config->doubleTap = editor->Action();
            _dirty();
        });

        Refresh();
    }

    void DoublePressPage::Refresh()
    {
        bool on = _config->doubleTapEnabled;
        int ms = _config->doubleTapWindowMs;

        Window().IsEnabled(on);
        WindowCard().Description(::AntiPilot::Shell::Strings::Format(L"DoubleTapMilliseconds", ms));
        EditorCard().IsHitTestVisible(on);
        EditorCard().Opacity(on ? 1.0 : 0.5);
        Warning().Text(on
            ? ::AntiPilot::Shell::Strings::Format(L"DoubleTapCostWarning", ms)
            : ::AntiPilot::Shell::Strings::Get(L"DoubleTapDisabledHint"));
    }

    // XAML raises these from its own deferred work as well as from the user, and the first of those
    // can land before Load has run, so a page with no config yet simply ignores them.
    void DoublePressPage::OnEnabledToggled(IInspectable const&, RoutedEventArgs const&)
    {
        if (_loading || !_config)
        {
            return;
        }

        _config->doubleTapEnabled = Enabled().IsOn();
        Refresh();
        _dirty();
    }

    void DoublePressPage::OnWindowChanged(IInspectable const&, Controls::Primitives::RangeBaseValueChangedEventArgs const& e)
    {
        int value = static_cast<int>(e.NewValue());
        if (_loading || !_config || value == _config->doubleTapWindowMs)
        {
            return;
        }

        _config->doubleTapWindowMs = value;
        Refresh();
        _dirty();
    }
}
