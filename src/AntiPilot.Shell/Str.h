#pragma once

#include "Str.g.h"

namespace winrt::AntiPilot::Shell::implementation
{
    struct Str : StrT<Str>
    {
        Str() = default;

        hstring Key() const { return _key; }
        void Key(hstring const& value) { _key = value; }

        // The base calls both; the second is the one XAML uses, the first exists for callers.
        Windows::Foundation::IInspectable ProvideValue();
        Windows::Foundation::IInspectable ProvideValue(Microsoft::UI::Xaml::IXamlServiceProvider const&) { return ProvideValue(); }

    private:
        hstring _key;
    };
}

namespace winrt::AntiPilot::Shell::factory_implementation
{
    struct Str : StrT<Str, implementation::Str>
    {
    };
}
