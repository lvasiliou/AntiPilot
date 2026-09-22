#include "pch.h"
#include "Apps.h"

#include "Log.h"
#include "Text.h"

#include <appmodel.h>
#include <knownfolders.h>
#include <shobjidl_core.h>
#include <shlobj.h>
#include <MemoryBuffer.h>
#include <winrt/Windows.Foundation.h>

#include <algorithm>
#include <optional>

using namespace ::AntiPilot;
using namespace winrt::Windows::Graphics::Imaging;

namespace
{
    // The names AntiPilot's own manifest entries carry, in the languages they are shown in.
    constexpr std::wstring_view OwnNames[] = { L"AntiPilot", L"AntiPilot Settings", L"AntiPilot tray icon", L"AntiPilot Settings (preview)" };

    std::optional<std::wstring> OwnFamily()
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
        return family;
    }

    // A sideload or Store build of this app, whichever the package family: AntiPilot_<13 chars>!,
    // at the start of the parsing name or after a publisher prefix and a dot.
    bool LooksLikeOurFamily(std::wstring_view parsingName)
    {
        constexpr std::wstring_view marker = L"AntiPilot_";
        for (size_t at = parsingName.find(marker); at != std::wstring_view::npos; at = parsingName.find(marker, at + 1))
        {
            if (at != 0 && parsingName[at - 1] != L'.')
            {
                continue;
            }

            size_t hash = at + marker.size();
            if (parsingName.size() <= hash + 13 || parsingName[hash + 13] != L'!')
            {
                continue;
            }

            bool ok = true;
            for (size_t i = hash; i < hash + 13 && ok; i++)
            {
                wchar_t c = parsingName[i];
                ok = (c >= L'a' && c <= L'z') || (c >= L'0' && c <= L'9');
            }

            if (ok)
            {
                return true;
            }
        }

        return false;
    }

    bool IsSelf(std::wstring_view name, std::wstring_view parsingName)
    {
        static const std::optional<std::wstring> family = OwnFamily();
        if (family && Text::StartsWithIgnoreCase(parsingName, *family + L"!"))
        {
            return true;
        }

        if (LooksLikeOurFamily(parsingName))
        {
            return true;
        }

        return std::any_of(std::begin(OwnNames), std::end(OwnNames), [&](auto own) { return Text::EqualsIgnoreCase(name, own); });
    }

    winrt::com_ptr<IShellItem> ItemFor(std::wstring_view parsingName)
    {
        winrt::com_ptr<IShellItem> item;
        std::wstring path = L"shell:AppsFolder\\" + std::wstring{ parsingName };
        SHCreateItemFromParsingName(path.c_str(), nullptr, IID_PPV_ARGS(item.put()));
        return item;
    }

    std::wstring DisplayName(IShellItem* item, SIGDN kind)
    {
        wchar_t* text = nullptr;
        if (FAILED(item->GetDisplayName(kind, &text)) || !text)
        {
            return {};
        }

        std::wstring result = text;
        CoTaskMemFree(text);
        return result;
    }
}

namespace AntiPilot::Shell::Apps
{
    std::vector<Entry> Enumerate()
    {
        std::vector<Entry> result;

        winrt::com_ptr<IShellItem> folder;
        if (FAILED(SHGetKnownFolderItem(FOLDERID_AppsFolder, KF_FLAG_DEFAULT, nullptr, IID_PPV_ARGS(folder.put()))))
        {
            Log::Write(L"Could not open the Apps folder; cannot list installed apps.");
            return result;
        }

        winrt::com_ptr<IEnumShellItems> items;
        if (FAILED(folder->BindToHandler(nullptr, BHID_EnumItems, IID_PPV_ARGS(items.put()))))
        {
            return result;
        }

        winrt::com_ptr<IShellItem> item;
        while (items->Next(1, item.put(), nullptr) == S_OK)
        {
            std::wstring name = DisplayName(item.get(), SIGDN_NORMALDISPLAY);
            std::wstring parsing = DisplayName(item.get(), SIGDN_PARENTRELATIVEPARSING);
            item = nullptr;

            if (!Text::IsBlank(name) && !Text::IsBlank(parsing) && !IsSelf(name, parsing))
            {
                result.push_back({ std::move(name), std::move(parsing) });
            }
        }

        std::sort(result.begin(), result.end(), [](Entry const& a, Entry const& b)
        {
            return CompareStringOrdinal(a.name.c_str(), -1, b.name.c_str(), -1, TRUE) == CSTR_LESS_THAN;
        });
        return result;
    }

    bool Exists(std::wstring_view parsingName)
    {
        return ItemFor(parsingName) != nullptr;
    }

    SoftwareBitmap TryGetIcon(std::wstring_view parsingName, int size)
    {
        auto item = ItemFor(parsingName);
        if (!item)
        {
            return nullptr;
        }

        auto factory = item.try_as<IShellItemImageFactory>();
        if (!factory)
        {
            return nullptr;
        }

        HBITMAP bitmap = nullptr;
        if (FAILED(factory->GetImage(SIZE{ size, size }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, &bitmap)) || !bitmap)
        {
            return nullptr;
        }

        SoftwareBitmap result{ nullptr };
        BITMAP info{};
        if (GetObjectW(bitmap, sizeof(info), &info) && info.bmBitsPixel == 32 && info.bmWidth > 0 && info.bmHeight > 0)
        {
            // Ask GDI for the pixels top-down, whichever way the DIB section stores them; this is
            // the orientation bug from the WinForms picker, avoided rather than fixed after the fact.
            BITMAPINFO header{};
            header.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
            header.bmiHeader.biWidth = info.bmWidth;
            header.bmiHeader.biHeight = -info.bmHeight;
            header.bmiHeader.biPlanes = 1;
            header.bmiHeader.biBitCount = 32;
            header.bmiHeader.biCompression = BI_RGB;

            std::vector<uint8_t> pixels(static_cast<size_t>(info.bmWidth) * info.bmHeight * 4);
            HDC dc = GetDC(nullptr);
            int lines = GetDIBits(dc, bitmap, 0, info.bmHeight, pixels.data(), &header, DIB_RGB_COLORS);
            ReleaseDC(nullptr, dc);

            if (lines == info.bmHeight)
            {
                result = SoftwareBitmap(BitmapPixelFormat::Bgra8, info.bmWidth, info.bmHeight, BitmapAlphaMode::Premultiplied);
                auto buffer = result.LockBuffer(BitmapBufferAccessMode::Write);
                auto reference = buffer.CreateReference();
                uint8_t* target = nullptr;
                uint32_t capacity = 0;
                reference.as<::Windows::Foundation::IMemoryBufferByteAccess>()->GetBuffer(&target, &capacity);
                auto layout = buffer.GetPlaneDescription(0);
                for (int y = 0; y < info.bmHeight; y++)
                {
                    memcpy(target + static_cast<size_t>(y) * layout.Stride, pixels.data() + static_cast<size_t>(y) * info.bmWidth * 4, static_cast<size_t>(info.bmWidth) * 4);
                }
            }
        }

        DeleteObject(bitmap);
        return result;
    }
}
