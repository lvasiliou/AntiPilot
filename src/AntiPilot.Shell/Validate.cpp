#include "pch.h"
#include "Validate.h"

#include "Actions.h"
#include "Apps.h"
#include "Hotkey.h"
#include "Log.h"
#include "Strings.h"
#include "Text.h"

#include <winrt/Windows.Foundation.h>

using namespace ::AntiPilot;

namespace
{
    bool Exists(std::wstring const& path)
    {
        return GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
    }

    // A bare command such as "notepad" or "winget" is resolved against PATH at launch, so check
    // the same way rather than calling it missing.
    bool ResolvesOnPath(std::wstring const& command)
    {
        if (Text::IsPathRooted(command) || command.find(L'\\') != std::wstring::npos)
        {
            return false;
        }

        std::wstring extensions = Text::ExpandEnvironment(L"%PATHEXT%");
        if (extensions.empty() || extensions == L"%PATHEXT%")
        {
            extensions = L".EXE;.CMD;.BAT";
        }

        wchar_t found[MAX_PATH]{};
        for (size_t start = 0; start <= extensions.size();)
        {
            size_t end = extensions.find(L';', start);
            std::wstring extension = Text::Trim(extensions.substr(start, end == std::wstring::npos ? std::wstring::npos : end - start));
            if (!extension.empty() && SearchPathW(nullptr, command.c_str(), extension.c_str(), MAX_PATH, found, nullptr) > 0)
            {
                return true;
            }

            if (end == std::wstring::npos) break;
            start = end + 1;
        }

        return false;
    }

    bool FileTargetExists(std::wstring const& path)
    {
        std::wstring expanded = Text::Trim(Text::ExpandEnvironment(path));
        if (expanded.empty())
        {
            return false;
        }

        // A URL, a shell: location or an ms-settings: link has no file to look for, and the shell
        // is the only thing that can say whether it resolves. Take it on trust.
        try
        {
            winrt::Windows::Foundation::Uri uri{ expanded };
            if (!Text::EqualsIgnoreCase(uri.SchemeName(), L"file"))
            {
                return true;
            }
        }
        catch (winrt::hresult_error const&)
        {
            // Not a URI, which most paths are not.
        }

        return Exists(expanded) || ResolvesOnPath(expanded);
    }
}

namespace AntiPilot::Shell::Validate
{
    std::optional<winrt::hstring> Action(KeyAction const& action)
    {
        // "Nothing" is a choice, not an oversight, so it is the one kind that never complains.
        switch (action.kind)
        {
        case ActionKind::ShellApp:
            if (Text::IsBlank(action.aumid))
            {
                return Strings::Get(L"NoAppChosen");
            }
            return Apps::Exists(action.aumid) ? std::nullopt : std::optional{ Strings::Get(L"TargetMissingApp") };

        case ActionKind::File:
            if (Text::IsBlank(action.path))
            {
                return Strings::Get(L"TargetMissingFile");
            }
            return FileTargetExists(action.path) ? std::nullopt : std::optional{ Strings::Get(L"TargetMissingFile") };

        case ActionKind::Hotkey:
            return HotkeyDefinition::TryParse(action.hotkey) ? std::nullopt : std::optional{ Strings::Get(L"HotkeyInvalid") };

        default:
            return std::nullopt;
        }
    }

    std::vector<winrt::hstring> Config(AppConfig const& config)
    {
        std::vector<winrt::hstring> problems;
        auto check = [&](winrt::hstring const& where, KeyAction const& action)
        {
            if (auto problem = Action(action))
            {
                problems.push_back(winrt::hstring{ std::wstring{ where } + L": " + std::wstring{ *problem } });
            }
        };

        check(Strings::Get(L"TabSinglePress"), config.tap);
        if (config.doubleTapEnabled)
        {
            check(Strings::Get(L"TabDoublePress"), config.doubleTap);
        }

        for (auto const& rule : config.appRules)
        {
            check(rule.processName.empty() ? Strings::Get(L"TabPerApp") : winrt::hstring{ rule.processName }, rule.action);
        }

        for (auto const& entry : config.palette)
        {
            check(Actions::Describe(entry), entry);
        }

        return problems;
    }
}
