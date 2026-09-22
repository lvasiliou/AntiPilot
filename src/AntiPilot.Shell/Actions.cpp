#include "pch.h"
#include "Actions.h"

#include "Strings.h"
#include "Text.h"

#include <algorithm>
#include <map>

using namespace ::AntiPilot;

namespace
{
    std::wstring FileName(std::wstring_view path)
    {
        size_t cut = path.find_last_of(L"\\/");
        return std::wstring{ cut == std::wstring_view::npos ? path : path.substr(cut + 1) };
    }

    std::wstring ProcessName(DWORD processId)
    {
        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
        if (!process)
        {
            return {};
        }

        wchar_t path[MAX_PATH]{};
        DWORD length = MAX_PATH;
        std::wstring name = QueryFullProcessImageNameW(process, 0, path, &length) ? Text::FileNameWithoutExtension(path) : std::wstring{};
        CloseHandle(process);
        return name;
    }

    // A Store app's top-level window belongs to ApplicationFrameHost; the app itself owns a child.
    DWORD HostedProcess(HWND frame, DWORD framePid)
    {
        struct Search { DWORD framePid; DWORD found; } search{ framePid, 0 };
        EnumChildWindows(frame, [](HWND child, LPARAM param) -> BOOL
        {
            auto* s = reinterpret_cast<Search*>(param);
            DWORD pid = 0;
            GetWindowThreadProcessId(child, &pid);
            if (pid != s->framePid)
            {
                s->found = pid;
                return FALSE;
            }
            return TRUE;
        }, reinterpret_cast<LPARAM>(&search));
        return search.found ? search.found : framePid;
    }

    bool IsCandidateWindow(HWND window)
    {
        if (!IsWindowVisible(window) || GetWindowTextLengthW(window) == 0 || GetWindow(window, GW_OWNER) != nullptr)
        {
            return false;
        }

        return (GetWindowLongPtrW(window, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) == 0;
    }
}

namespace AntiPilot::Shell::Actions
{
    winrt::hstring Describe(KeyAction const& action)
    {
        if (!Text::IsBlank(action.label))
        {
            return winrt::hstring{ action.label };
        }

        switch (action.kind)
        {
        case ActionKind::ShellApp:
            return Text::IsBlank(action.displayName)
                ? (action.aumid.empty() ? Strings::Get(L"NoApp") : winrt::hstring{ action.aumid })
                : winrt::hstring{ action.displayName };
        case ActionKind::File:
            return Text::IsBlank(action.path) ? Strings::Get(L"Nothing") : winrt::hstring{ FileName(action.path) };
        case ActionKind::MenuKey:
            return Strings::Get(L"MenuKeyShort");
        case ActionKind::Hotkey:
            return Text::IsBlank(action.hotkey) ? Strings::Get(L"Nothing") : winrt::hstring{ action.hotkey };
        case ActionKind::Palette:
            return Strings::Get(L"PaletteShort");
        default:
            return Strings::Get(L"Nothing");
        }
    }

    winrt::hstring DescribeTarget(KeyAction const& action)
    {
        switch (action.kind)
        {
        case ActionKind::ShellApp: return winrt::hstring{ action.displayName.empty() ? action.aumid : action.displayName };
        case ActionKind::File: return winrt::hstring{ action.path };
        case ActionKind::Hotkey: return winrt::hstring{ action.hotkey };
        case ActionKind::MenuKey: return Strings::Get(L"MenuKeyShort");
        default: return {};
        }
    }

    std::vector<std::pair<std::wstring, std::wstring>> ListWindowedApps()
    {
        std::map<std::wstring, std::wstring> seen; // process name -> first title, sorted for the menu
        EnumWindows([](HWND window, LPARAM param) -> BOOL
        {
            auto* apps = reinterpret_cast<std::map<std::wstring, std::wstring>*>(param);
            if (!IsCandidateWindow(window))
            {
                return TRUE;
            }

            DWORD pid = 0;
            GetWindowThreadProcessId(window, &pid);
            std::wstring name = ProcessName(pid);
            if (Text::EqualsIgnoreCase(name, L"ApplicationFrameHost"))
            {
                name = ProcessName(HostedProcess(window, pid));
            }

            if (name.empty() || apps->contains(name))
            {
                return TRUE;
            }

            wchar_t title[256]{};
            GetWindowTextW(window, title, 256);
            (*apps)[name] = title;
            return TRUE;
        }, reinterpret_cast<LPARAM>(&seen));

        return { seen.begin(), seen.end() };
    }
}
