#include "Launch.h"

#include "Log.h"
#include "Paths.h"
#include "Text.h"

#include <Windows.h>
#include <shellapi.h>
#include <shobjidl.h>

#include <format>

namespace AntiPilot::Launch
{
    namespace
    {
        /// <summary>COM for the lifetime of one launch. The key path pays for it only when it needs it.</summary>
        class ComScope
        {
        public:
            ComScope()
            {
                HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
                _initialised = SUCCEEDED(hr);
            }

            ~ComScope()
            {
                if (_initialised)
                {
                    CoUninitialize();
                }
            }

            ComScope(const ComScope&) = delete;
            ComScope& operator=(const ComScope&) = delete;

        private:
            bool _initialised = false;
        };

        /// <summary>
        /// One argument as CommandLineToArgvW will read it back: quoted when it has to be, with
        /// embedded quotes and the backslashes that precede them escaped.
        /// </summary>
        std::wstring QuoteArgument(std::wstring_view argument)
        {
            if (!argument.empty() && argument.find_first_of(L" \t\"") == std::wstring_view::npos)
            {
                return std::wstring(argument);
            }

            std::wstring quoted = L"\"";
            size_t backslashes = 0;
            for (wchar_t c : argument)
            {
                if (c == L'\\')
                {
                    ++backslashes;
                    continue;
                }

                if (c == L'"')
                {
                    quoted.append(backslashes * 2 + 1, L'\\');
                    quoted += L'"';
                }
                else
                {
                    quoted.append(backslashes, L'\\');
                    quoted += c;
                }

                backslashes = 0;
            }

            quoted.append(backslashes * 2, L'\\');
            quoted += L'"';
            return quoted;
        }

        std::wstring ShellError(std::wstring_view what, std::wstring_view target)
        {
            return std::format(L"{} '{}': {}", what, target, Text::DescribeError(GetLastError()));
        }

        bool RunShell(std::wstring_view file, std::wstring_view parameters, std::wstring_view directory)
        {
            SHELLEXECUTEINFOW info{};
            info.cbSize = sizeof(info);
            info.fMask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI;
            info.lpFile = file.data();
            info.lpParameters = parameters.empty() ? nullptr : parameters.data();
            info.lpDirectory = directory.empty() ? nullptr : directory.data();
            info.nShow = SW_SHOWNORMAL;
            return ShellExecuteExW(&info) != FALSE;
        }
    }

    std::wstring AppsFolderItem(std::wstring_view parsingName)
    {
        ComScope com;
        std::wstring name(parsingName);

        if (name.find(L'!') != std::wstring::npos)
        {
            IApplicationActivationManager* manager = nullptr;
            HRESULT hr = CoCreateInstance(__uuidof(ApplicationActivationManager), nullptr, CLSCTX_LOCAL_SERVER,
                IID_PPV_ARGS(&manager));

            if (SUCCEEDED(hr) && manager != nullptr)
            {
                DWORD pid = 0;
                hr = manager->ActivateApplication(name.c_str(), nullptr, AO_NONE, &pid);
                manager->Release();

                if (SUCCEEDED(hr))
                {
                    Log::Write(std::format(L"Activated '{}' (pid {}).", name, pid));
                    return {};
                }

                Log::Write(std::format(L"ActivateApplication('{}') failed with 0x{:08X}; falling back to the shell.", name, static_cast<unsigned long>(hr)));
            }
            else
            {
                Log::Write(std::format(L"The activation manager is unavailable (0x{:08X}); falling back to the shell.", static_cast<unsigned long>(hr)));
            }
        }

        // Works for both packaged and classic Start-menu entries.
        std::wstring parameters = L"shell:AppsFolder\\" + name;
        if (!RunShell(L"explorer.exe", parameters, {}))
        {
            return ShellError(L"Could not open", name);
        }

        return {};
    }

    std::wstring File(const KeyAction& action)
    {
        ComScope com;

        std::wstring fileName = Text::ExpandEnvironment(action.path);
        std::wstring arguments = Text::IsBlank(action.arguments) ? std::wstring() : Text::ExpandEnvironment(action.arguments);

        std::wstring directory;
        if (!Text::IsBlank(action.workingDirectory))
        {
            directory = Text::ExpandEnvironment(action.workingDirectory);
        }
        else if (Text::IsPathRooted(fileName))
        {
            std::wstring parent = Text::DirectoryName(fileName);
            DWORD attributes = parent.empty() ? INVALID_FILE_ATTRIBUTES : GetFileAttributesW(parent.c_str());
            if (attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
            {
                directory = parent;
            }
        }

        Log::Write(std::format(L"Launching '{}' {}", fileName, arguments));

        if (!RunShell(fileName, arguments, directory))
        {
            return ShellError(L"Could not start", fileName);
        }

        return {};
    }

    bool Delegate(std::initializer_list<std::wstring_view> arguments)
    {
        std::wstring exe = Paths::Sibling(L"AntiPilot.exe");

        std::wstring commandLine = QuoteArgument(exe);
        for (std::wstring_view argument : arguments)
        {
            commandLine += L' ';
            commandLine += QuoteArgument(argument);
        }

        STARTUPINFOW startup{};
        startup.cb = sizeof(startup);
        PROCESS_INFORMATION process{};

        // CreateProcessW may write to the command line, so it cannot be handed a literal.
        std::wstring mutableCommandLine = commandLine;
        BOOL started = CreateProcessW(exe.c_str(), mutableCommandLine.data(), nullptr, nullptr, FALSE,
            0, nullptr, nullptr, &startup, &process);

        if (!started)
        {
            Log::Write(std::format(L"Could not start '{}': {}", commandLine, Text::DescribeError(GetLastError())));
            return false;
        }

        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        return true;
    }
}
