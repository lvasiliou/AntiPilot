#include "Paths.h"

#include <Windows.h>
#include <ShlObj.h>

namespace AntiPilot::Paths
{
    std::wstring ConfigDirectory()
    {
        wchar_t* folder = nullptr;
        std::wstring result;

        if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_DEFAULT, nullptr, &folder)) && folder != nullptr)
        {
            result = folder;
        }

        if (folder != nullptr)
        {
            CoTaskMemFree(folder);
        }

        if (result.empty())
        {
            // The known-folder API failing is a broken profile, but a log line is still worth trying for.
            wchar_t fallback[MAX_PATH];
            DWORD length = GetEnvironmentVariableW(L"LOCALAPPDATA", fallback, MAX_PATH);
            if (length > 0 && length < MAX_PATH)
            {
                result = fallback;
            }
        }

        return result + L"\\AntiPilot";
    }

    std::wstring ConfigPath()
    {
        return ConfigDirectory() + L"\\config.json";
    }

    std::wstring OwnDirectory()
    {
        std::wstring path(MAX_PATH, L'\0');
        while (true)
        {
            DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
            if (length == 0)
            {
                return {};
            }

            if (length < path.size())
            {
                path.resize(length);
                break;
            }

            path.resize(path.size() * 2);
        }

        size_t slash = path.find_last_of(L'\\');
        return slash == std::wstring::npos ? std::wstring() : path.substr(0, slash);
    }

    std::wstring Sibling(std::wstring_view fileName)
    {
        return OwnDirectory() + L"\\" + std::wstring(fileName);
    }

    bool EnsureDirectory(const std::wstring& path)
    {
        // SHCreateDirectoryExW creates the intermediate folders too, which CreateDirectoryW will not.
        int result = SHCreateDirectoryExW(nullptr, path.c_str(), nullptr);
        return result == ERROR_SUCCESS || result == ERROR_ALREADY_EXISTS || result == ERROR_FILE_EXISTS;
    }
}
