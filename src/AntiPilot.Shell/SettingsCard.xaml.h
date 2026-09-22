#pragma once

#include "SettingsCard.g.h"

namespace winrt::AntiPilot::Shell::implementation
{
    struct SettingsCard : SettingsCardT<SettingsCard>
    {
        SettingsCard() = default;

        void InitializeComponent();

        hstring Glyph() const { return _glyph; }
        void Glyph(hstring const& value) { _glyph = value; Apply(); }

        hstring Title() const { return _title; }
        void Title(hstring const& value) { _title = value; Apply(); }

        hstring Description() const { return _description; }
        void Description(hstring const& value) { _description = value; Apply(); }

        Microsoft::UI::Xaml::UIElement Action() const { return _action; }
        void Action(Microsoft::UI::Xaml::UIElement const& value) { _action = value; Apply(); }

        bool Flat() const { return _flat; }
        void Flat(bool value) { _flat = value; Apply(); }

    private:
        hstring _glyph;
        hstring _title;
        hstring _description;
        Microsoft::UI::Xaml::UIElement _action{ nullptr };
        bool _flat = false;
        bool _ready = false;

        // Markup may set the properties before or after the visual tree exists, so every setter
        // records the value and the tree is brought up to date whenever both are there.
        void Apply();
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct SettingsCard : SettingsCardT<SettingsCard, implementation::SettingsCard>
    {
    };
}
