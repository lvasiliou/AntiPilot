#include "pch.h"
#include "Strings.h"

#include <winrt/Microsoft.Windows.ApplicationModel.Resources.h>

using namespace winrt::Microsoft::Windows::ApplicationModel::Resources;

namespace AntiPilot::Shell::Strings
{
    winrt::hstring Get(std::wstring_view key)
    {
        // The package's resources.pri, "Resources" map: what makepri builds from Strings\<lang>\.
        // ponytail: follows the Windows display language; the language chosen in settings is not
        // applied yet. Add a ResourceContext with its Language qualifier when the General page lands.
        static ResourceLoader loader;

        try
        {
            auto value = loader.GetString(key);
            if (!value.empty())
            {
                return value;
            }
        }
        catch (winrt::hresult_error const&)
        {
        }

        return winrt::hstring{ L"!" + std::wstring{ key } + L"!" };
    }
}
