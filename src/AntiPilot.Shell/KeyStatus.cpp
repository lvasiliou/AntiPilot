#include "pch.h"
#include "KeyStatus.h"

#include "Log.h"
#include "Strings.h"
#include "Text.h"

#include <appmodel.h>
#include <winrt/Windows.System.h>

#include <optional>
#include <string>

namespace
{
    constexpr wchar_t BrandedKey[] = L"Software\\Microsoft\\Windows\\Shell\\BrandedKey";

    std::optional<std::wstring> ReadValue(const wchar_t* name)
    {
        DWORD size = 0;
        if (RegGetValueW(HKEY_CURRENT_USER, BrandedKey, name, RRF_RT_REG_SZ, nullptr, nullptr, &size) != ERROR_SUCCESS || size < sizeof(wchar_t))
        {
            return std::nullopt;
        }

        std::wstring value(size / sizeof(wchar_t), L'\0');
        if (RegGetValueW(HKEY_CURRENT_USER, BrandedKey, name, RRF_RT_REG_SZ, nullptr, value.data(), &size) != ERROR_SUCCESS)
        {
            return std::nullopt;
        }

        value.resize(wcslen(value.c_str()));
        return value;
    }

    std::optional<std::wstring> OwnAumid()
    {
        UINT32 length = 0;
        if (GetCurrentPackageFamilyName(&length, nullptr) == APPMODEL_ERROR_NO_PACKAGE)
        {
            return std::nullopt;
        }

        std::wstring family(length, L'\0');
        if (GetCurrentPackageFamilyName(&length, family.data()) != ERROR_SUCCESS)
        {
            return std::nullopt;
        }

        family.resize(wcslen(family.c_str()));
        return family + L"!AntiPilot";
    }
}

namespace AntiPilot::Shell::KeyStatus
{
    winrt::hstring Describe()
    {
        auto own = OwnAumid();
        if (!own)
        {
            return Strings::Get(L"StatusUnpackaged");
        }

        auto choice = ReadValue(L"BrandedKeyChoiceType").value_or(L"");
        bool pointsAtAnApp = Text::EqualsIgnoreCase(choice, L"App") || Text::EqualsIgnoreCase(choice, L"AppEnforcedByPolicy");

        if (pointsAtAnApp && Text::EqualsIgnoreCase(ReadValue(L"AppAumid").value_or(L""), *own))
        {
            return Strings::Get(L"StatusActive");
        }

        if (Text::EqualsIgnoreCase(choice, L"Search"))
        {
            return Strings::Get(L"StatusSearch");
        }

        return Strings::Get(pointsAtAnApp ? L"StatusOtherApp" : L"StatusDefault");
    }

    winrt::fire_and_forget OpenWindowsSettings()
    {
        // The deep link straight to the dropdown, then the page it sits on, then where it used to live.
        for (auto uri : { L"ms-settings:personalization-textinput-copilot-hardwarekey", L"ms-settings:keyboard", L"ms-settings:personalization-textinput" })
        {
            if (co_await winrt::Windows::System::Launcher::LaunchUriAsync(winrt::Windows::Foundation::Uri{ uri }))
            {
                co_return;
            }

            Log::Write(std::wstring(L"Could not open ") + uri);
        }
    }
}
