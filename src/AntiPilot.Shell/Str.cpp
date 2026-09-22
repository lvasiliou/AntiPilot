#include "pch.h"
#include "Str.h"
#if __has_include("Str.g.cpp")
#include "Str.g.cpp"
#endif

#include "Strings.h"

namespace winrt::AntiPilot::Shell::implementation
{
    Windows::Foundation::IInspectable Str::ProvideValue()
    {
        // Fully qualified: inside this namespace, AntiPilot:: resolves to winrt::AntiPilot::.
        return box_value(::AntiPilot::Shell::Strings::Get(_key));
    }
}
