#include "Text.h"

#include <Windows.h>

#include <algorithm>
#include <cwctype>

namespace AntiPilot::Text
{
    std::wstring FromUtf8(std::string_view utf8)
    {
        if (utf8.empty())
        {
            return {};
        }

        int length = MultiByteToWideChar(CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), nullptr, 0);
        if (length <= 0)
        {
            return {};
        }

        std::wstring result(static_cast<size_t>(length), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), result.data(), length);
        return result;
    }

    std::string ToUtf8(std::wstring_view text)
    {
        if (text.empty())
        {
            return {};
        }

        int length = WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
        if (length <= 0)
        {
            return {};
        }

        std::string result(static_cast<size_t>(length), '\0');
        WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), result.data(), length, nullptr, nullptr);
        return result;
    }

    bool EqualsIgnoreCase(std::wstring_view left, std::wstring_view right)
    {
        if (left.size() != right.size())
        {
            return false;
        }

        return CompareStringOrdinal(
            left.data(), static_cast<int>(left.size()),
            right.data(), static_cast<int>(right.size()),
            TRUE) == CSTR_EQUAL;
    }

    bool EndsWithIgnoreCase(std::wstring_view text, std::wstring_view suffix)
    {
        return text.size() >= suffix.size() &&
            EqualsIgnoreCase(text.substr(text.size() - suffix.size()), suffix);
    }

    bool StartsWithIgnoreCase(std::wstring_view text, std::wstring_view prefix)
    {
        return text.size() >= prefix.size() && EqualsIgnoreCase(text.substr(0, prefix.size()), prefix);
    }

    std::wstring Trim(std::wstring_view text)
    {
        size_t start = 0;
        while (start < text.size() && std::iswspace(text[start]))
        {
            ++start;
        }

        size_t end = text.size();
        while (end > start && std::iswspace(text[end - 1]))
        {
            --end;
        }

        return std::wstring(text.substr(start, end - start));
    }

    bool IsBlank(std::wstring_view text)
    {
        return std::all_of(text.begin(), text.end(), [](wchar_t c) { return std::iswspace(c) != 0; });
    }

    std::wstring ToLowerInvariant(std::wstring_view text)
    {
        std::wstring result(text);
        if (!result.empty())
        {
            // LCMAP_LOWERCASE with the invariant locale, so a Turkish user's dotted I does not turn
            // "CTRL" into something the parser has never heard of.
            LCMapStringEx(LOCALE_NAME_INVARIANT, LCMAP_LOWERCASE,
                result.data(), static_cast<int>(result.size()),
                result.data(), static_cast<int>(result.size()),
                nullptr, nullptr, 0);
        }

        return result;
    }

    std::wstring ExpandEnvironment(std::wstring_view text)
    {
        std::wstring source(text);
        DWORD needed = ExpandEnvironmentStringsW(source.c_str(), nullptr, 0);
        if (needed == 0)
        {
            return source;
        }

        std::wstring result(needed, L'\0');
        DWORD written = ExpandEnvironmentStringsW(source.c_str(), result.data(), needed);
        if (written == 0 || written > needed)
        {
            return source;
        }

        // The count includes the terminator.
        result.resize(written - 1);
        return result;
    }

    std::wstring FileNameWithoutExtension(std::wstring_view path)
    {
        size_t slash = path.find_last_of(L"\\/");
        std::wstring_view name = slash == std::wstring_view::npos ? path : path.substr(slash + 1);

        size_t dot = name.rfind(L'.');
        if (dot != std::wstring_view::npos)
        {
            name = name.substr(0, dot);
        }

        return std::wstring(name);
    }

    std::wstring DirectoryName(std::wstring_view path)
    {
        size_t slash = path.find_last_of(L"\\/");
        return slash == std::wstring_view::npos ? std::wstring() : std::wstring(path.substr(0, slash));
    }

    bool IsPathRooted(std::wstring_view path)
    {
        if (path.size() >= 2 && path[1] == L':')
        {
            return true;
        }

        return !path.empty() && (path[0] == L'\\' || path[0] == L'/');
    }

    std::wstring DescribeError(unsigned long error)
    {
        wchar_t* buffer = nullptr;
        DWORD length = FormatMessageW(
            FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
            nullptr, error, 0, reinterpret_cast<wchar_t*>(&buffer), 0, nullptr);

        std::wstring result = length > 0 && buffer != nullptr ? Trim({ buffer, length }) : std::wstring();
        if (buffer != nullptr)
        {
            LocalFree(buffer);
        }

        if (result.empty())
        {
            result = L"error " + std::to_wstring(error);
        }

        return result;
    }
}
