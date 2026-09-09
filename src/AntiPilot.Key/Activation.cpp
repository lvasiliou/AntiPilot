#include "Activation.h"

#include "Text.h"

#include <Windows.h>
#include <appmodel.h>
#include <shellapi.h>

namespace AntiPilot::Activation
{
    namespace
    {
        constexpr std::wstring_view Scheme = L"antipilot-key:";

        /// <summary>The value of one query-string key, or empty. Enough for "state=Down"; nothing is percent-encoded.</summary>
        std::wstring QueryValue(std::wstring_view uri, std::wstring_view key)
        {
            size_t question = uri.find(L'?');
            if (question == std::wstring_view::npos)
            {
                return {};
            }

            std::wstring_view query = uri.substr(question + 1);
            while (!query.empty())
            {
                size_t amp = query.find(L'&');
                std::wstring_view pair = amp == std::wstring_view::npos ? query : query.substr(0, amp);
                query = amp == std::wstring_view::npos ? std::wstring_view() : query.substr(amp + 1);

                size_t equals = pair.find(L'=');
                if (equals == std::wstring_view::npos)
                {
                    continue;
                }

                if (Text::EqualsIgnoreCase(pair.substr(0, equals), key))
                {
                    return std::wstring(pair.substr(equals + 1));
                }
            }

            return {};
        }
    }

    std::vector<std::wstring> Arguments()
    {
        std::vector<std::wstring> result;

        int count = 0;
        wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &count);
        if (argv == nullptr)
        {
            return result;
        }

        for (int i = 1; i < count; ++i)
        {
            result.emplace_back(argv[i]);
        }

        LocalFree(argv);
        return result;
    }

    std::wstring State(const std::vector<std::wstring>& arguments)
    {
        for (const std::wstring& argument : arguments)
        {
            if (Text::StartsWithIgnoreCase(argument, Scheme))
            {
                std::wstring state = QueryValue(argument, L"state");
                return state.empty() ? std::wstring(L"Tap") : state;
            }
        }

        return L"Tap";
    }

    std::wstring CurrentAumid()
    {
        UINT32 length = 0;
        if (GetCurrentApplicationUserModelId(&length, nullptr) != ERROR_INSUFFICIENT_BUFFER || length == 0)
        {
            return {};
        }

        std::wstring result(length, L'\0');
        if (GetCurrentApplicationUserModelId(&length, result.data()) != ERROR_SUCCESS)
        {
            return {};
        }

        result.resize(length > 0 ? length - 1 : 0);
        return result;
    }
}
