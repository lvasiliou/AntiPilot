#include "pch.h"
#include "Startup.h"

#include "Launch.h"
#include "Log.h"
#include "Paths.h"
#include "Text.h"

#include <appmodel.h>
#include <knownfolders.h>
#include <shlobj.h>
#include <shobjidl.h>

#include <format>
#include <optional>
#include <string>

namespace
{
    constexpr wchar_t ShortcutName[] = L"AntiPilot tray icon.lnk";
    constexpr wchar_t ApprovalKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\StartupFolder";
    constexpr wchar_t TrayMutex[] = L"Local\\AntiPilot.TrayIcon";
    constexpr wchar_t TrayExitEvent[] = L"Local\\AntiPilot.TrayExit";

    std::wstring ShortcutPath()
    {
        wchar_t* folder = nullptr;
        if (FAILED(SHGetKnownFolderPath(FOLDERID_Startup, 0, nullptr, &folder)))
        {
            return {};
        }

        std::wstring path = std::wstring(folder) + L"\\" + ShortcutName;
        CoTaskMemFree(folder);
        return path;
    }

    std::optional<std::wstring> TrayAumid()
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
        return family + L"!Tray";
    }

    bool Exists(const std::wstring& path)
    {
        return !path.empty() && GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
    }

    // Task Manager records its own verdict next to the shortcut; an even first byte means enabled,
    // odd means the user switched it off there and Explorer will ignore the shortcut.
    bool IsApprovedByUser()
    {
        BYTE value[16]{};
        DWORD size = sizeof(value);
        if (RegGetValueW(HKEY_CURRENT_USER, ApprovalKey, ShortcutName, RRF_RT_REG_BINARY, nullptr, value, &size) == ERROR_SUCCESS && size > 0)
        {
            return (value[0] & 1) == 0;
        }

        return true; // No entry means Explorer has not been told to disable it.
    }

    HRESULT WriteShortcut(const std::wstring& path, const std::wstring& target, const std::wstring& arguments, const std::wstring& iconSource)
    {
        winrt::com_ptr<IShellLinkW> link;
        HRESULT hr = CoCreateInstance(CLSID_ShellLink, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(link.put()));
        if (FAILED(hr)) return hr;

        if (FAILED(hr = link->SetPath(target.c_str()))) return hr;
        if (FAILED(hr = link->SetArguments(arguments.c_str()))) return hr;
        if (FAILED(hr = link->SetDescription(L"Starts the AntiPilot notification-area icon."))) return hr;
        if (!iconSource.empty() && FAILED(hr = link->SetIconLocation(iconSource.c_str(), 0))) return hr;

        return link.as<IPersistFile>()->Save(path.c_str(), TRUE);
    }
}

namespace AntiPilot::Shell::Startup
{
    Availability GetState()
    {
        if (!TrayAumid())
        {
            return Availability::Unavailable;
        }

        if (!Exists(ShortcutPath()))
        {
            return Availability::Off;
        }

        return IsApprovedByUser() ? Availability::On : Availability::BlockedByUser;
    }

    Availability Enable()
    {
        auto aumid = TrayAumid();
        if (!aumid)
        {
            return Availability::Unavailable;
        }

        // explorer.exe + AUMID rather than the exe path: the install path carries the package
        // version, so a direct path would break on every update. The icon comes from AntiPilot.exe,
        // which has one; this executable does not.
        wchar_t windows[MAX_PATH]{};
        GetWindowsDirectoryW(windows, MAX_PATH);
        const std::wstring path = ShortcutPath();

        HRESULT hr = WriteShortcut(path, std::wstring(windows) + L"\\explorer.exe", L"shell:AppsFolder\\" + *aumid, Paths::Sibling(L"AntiPilot.exe"));
        if (FAILED(hr))
        {
            Log::Write(std::format(L"Could not create the startup shortcut: 0x{:08x}", static_cast<unsigned long>(hr)));
            return Availability::Off;
        }

        Log::Write(L"Startup shortcut written to " + path);
        return GetState();
    }

    Availability Disable()
    {
        const std::wstring path = ShortcutPath();
        if (Exists(path))
        {
            if (DeleteFileW(path.c_str()))
            {
                Log::Write(L"Startup shortcut removed.");
            }
            else
            {
                Log::Write(L"Could not remove the startup shortcut: " + Text::DescribeError(GetLastError()));
            }
        }

        return GetState();
    }

    bool IsTrayRunning()
    {
        HANDLE mutex = OpenMutexW(SYNCHRONIZE, FALSE, TrayMutex);
        if (!mutex)
        {
            return false;
        }

        CloseHandle(mutex);
        return true;
    }

    bool StartTray()
    {
        if (IsTrayRunning())
        {
            return true;
        }

        bool started = Launch::Delegate({ L"--tray" });
        Log::Write(started ? L"Tray icon started from the settings window." : L"Could not start the tray icon.");
        return started;
    }

    void StopTray()
    {
        HANDLE signal = OpenEventW(EVENT_MODIFY_STATE, FALSE, TrayExitEvent);
        if (!signal)
        {
            return;
        }

        SetEvent(signal);
        CloseHandle(signal);
        Log::Write(L"Asked the tray icon process to exit.");
    }
}
