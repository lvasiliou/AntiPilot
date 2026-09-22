#pragma once

#include "Apps.h"

#include <memory>
#include <winrt/Microsoft.UI.Xaml.h>
#include <winrt/Windows.Foundation.h>

namespace AntiPilot::Shell::AppPicker
{
    // The searchable list of installed apps with their icons. True when the user picked one, in
    // which case <paramref name="picked"/> holds it. <paramref name="current"/> is preselected.
    winrt::Windows::Foundation::IAsyncOperation<bool> Show(
        winrt::Microsoft::UI::Xaml::XamlRoot root,
        std::wstring current,
        std::shared_ptr<Apps::Entry> picked);

    // The icon for one entry, as something an Image can show, or nullptr.
    winrt::Windows::Foundation::IAsyncOperation<winrt::Microsoft::UI::Xaml::Media::Imaging::SoftwareBitmapSource> IconFor(
        std::wstring parsingName, int size);
}
