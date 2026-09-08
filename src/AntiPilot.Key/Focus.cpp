#include "Focus.h"

#include "Log.h"
#include "Text.h"

#include <Windows.h>
#include <appmodel.h>

#include <format>
#include <optional>

namespace AntiPilot::Focus
{
    namespace
    {
        /// <summary>
        /// UWP and packaged-desktop apps get their top-level window from this process rather than
        /// their own, so a bare process lookup reports the host for half the Store. Both paths
        /// below step through it to the window's real owner.
        /// </summary>
        constexpr std::wstring_view FrameHost = L"ApplicationFrameHost";

        /// <summary>A window plus the process that really owns it.</summary>
        struct TopLevelWindow
        {
            HWND handle = nullptr;
            DWORD processId = 0;
            std::wstring processName;
            std::wstring aumid;
        };

        std::wstring ProcessName(DWORD processId)
        {
            if (processId == 0)
            {
                return {};
            }

            HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
            if (process == nullptr)
            {
                // Gone between the enumeration and here, or not ours to look at.
                return {};
            }

            wchar_t buffer[MAX_PATH * 2];
            DWORD length = static_cast<DWORD>(std::size(buffer));
            std::wstring name;
            if (QueryFullProcessImageNameW(process, 0, buffer, &length))
            {
                name = Text::FileNameWithoutExtension({ buffer, length });
            }

            CloseHandle(process);
            return name;
        }

        /// <summary>AUMID of another running process, or empty when it has no package identity.</summary>
        std::wstring ApplicationUserModelId(DWORD processId)
        {
            HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
            if (process == nullptr)
            {
                return {};
            }

            std::wstring result;
            UINT32 length = 0;
            if (GetApplicationUserModelId(process, &length, nullptr) == ERROR_INSUFFICIENT_BUFFER && length > 0)
            {
                result.resize(length);
                if (GetApplicationUserModelId(process, &length, result.data()) == ERROR_SUCCESS)
                {
                    // The count includes the terminator.
                    result.resize(length > 0 ? length - 1 : 0);
                }
                else
                {
                    result.clear();
                }
            }

            CloseHandle(process);
            return result;
        }

        /// <summary>
        /// The real app behind an ApplicationFrameHost window: its CoreWindow child belongs to a
        /// different process, which is the one the user thinks of as the app.
        /// </summary>
        DWORD FindHostedProcess(HWND frame, DWORD framePid)
        {
            struct Search
            {
                DWORD framePid;
                DWORD found;
            } search{ framePid, 0 };

            EnumChildWindows(frame, [](HWND child, LPARAM param) -> BOOL
            {
                auto* s = reinterpret_cast<Search*>(param);
                DWORD childPid = 0;
                GetWindowThreadProcessId(child, &childPid);
                if (childPid != 0 && childPid != s->framePid)
                {
                    s->found = childPid;
                    return FALSE;
                }

                return TRUE;
            }, reinterpret_cast<LPARAM>(&search));

            return search.found;
        }

        TopLevelWindow DescribeWindow(HWND handle)
        {
            TopLevelWindow window;
            window.handle = handle;
            GetWindowThreadProcessId(handle, &window.processId);
            window.processName = ProcessName(window.processId);

            if (Text::EqualsIgnoreCase(window.processName, FrameHost))
            {
                DWORD hosted = FindHostedProcess(handle, window.processId);
                if (hosted != 0)
                {
                    window.processId = hosted;
                    window.processName = ProcessName(hosted);
                }
            }

            window.aumid = ApplicationUserModelId(window.processId);
            return window;
        }

        /// <summary>Filters out the invisible, the owned and the tool windows nobody means by "the window".</summary>
        bool IsCandidateWindow(HWND handle)
        {
            if (!IsWindowVisible(handle) || GetWindowTextLengthW(handle) == 0)
            {
                return false;
            }

            if (GetWindow(handle, GW_OWNER) != nullptr)
            {
                return false;
            }

            return (GetWindowLongPtrW(handle, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) == 0;
        }

        /// <summary>
        /// Windows only lets the foreground process hand focus away, so SetForegroundWindow alone
        /// is unreliable from here. Attaching to the current foreground thread's input queue first
        /// is the long-standing way round it.
        /// </summary>
        void Activate(HWND window)
        {
            if (IsIconic(window))
            {
                ShowWindow(window, SW_RESTORE);
            }

            HWND foreground = GetForegroundWindow();
            DWORD us = GetCurrentThreadId();
            DWORD them = foreground == nullptr ? us : GetWindowThreadProcessId(foreground, nullptr);

            bool attached = them != us && AttachThreadInput(us, them, TRUE);

            BringWindowToTop(window);
            SetForegroundWindow(window);

            if (attached)
            {
                AttachThreadInput(us, them, FALSE);
            }
        }

        std::optional<TopLevelWindow> FindTargetWindow(std::wstring_view aumid, std::wstring_view path)
        {
            std::wstring wantedAumid;
            if (!Text::IsBlank(aumid) && aumid.find(L'!') != std::wstring_view::npos)
            {
                wantedAumid = aumid;
            }

            // A classic Start-menu entry parses to a path, and so does a File action; either way the
            // executable name is what a running window can be matched on.
            std::wstring_view candidate = wantedAumid.empty() ? (aumid.empty() ? path : aumid) : path;
            std::wstring wantedProcess;
            if (!Text::IsBlank(candidate))
            {
                std::wstring expanded = Text::ExpandEnvironment(candidate);
                if (Text::EndsWithIgnoreCase(expanded, L".exe"))
                {
                    wantedProcess = Text::FileNameWithoutExtension(expanded);
                }
            }

            if (wantedAumid.empty() && wantedProcess.empty())
            {
                return std::nullopt;
            }

            struct Search
            {
                std::wstring_view wantedAumid;
                std::wstring_view wantedProcess;
                std::optional<TopLevelWindow> best;
            } search{ wantedAumid, wantedProcess, std::nullopt };

            EnumWindows([](HWND handle, LPARAM param) -> BOOL
            {
                auto* s = reinterpret_cast<Search*>(param);
                if (!IsCandidateWindow(handle))
                {
                    return TRUE;
                }

                TopLevelWindow window = DescribeWindow(handle);
                bool matches =
                    (!s->wantedAumid.empty() && Text::EqualsIgnoreCase(window.aumid, s->wantedAumid)) ||
                    (!s->wantedProcess.empty() && Text::EqualsIgnoreCase(window.processName, s->wantedProcess));

                if (!matches)
                {
                    return TRUE;
                }

                s->best = window;

                // Prefer whatever is already on screen over a minimised window of the same app.
                return IsIconic(handle);
            }, reinterpret_cast<LPARAM>(&search));

            return search.best;
        }
    }

    std::wstring GetForegroundProcessName()
    {
        HWND window = GetForegroundWindow();
        if (window == nullptr)
        {
            return {};
        }

        return DescribeWindow(window).processName;
    }

    bool TryFocus(std::wstring_view aumid, std::wstring_view path, bool allowMinimise)
    {
        auto match = FindTargetWindow(aumid, path);
        if (!match)
        {
            return false;
        }

        if (allowMinimise && GetForegroundWindow() == match->handle && !IsIconic(match->handle))
        {
            ShowWindow(match->handle, SW_MINIMIZE);
            Log::Write(std::format(L"Minimised the already-focused window of '{}'.", match->processName));
            return true;
        }

        Activate(match->handle);
        Log::Write(std::format(L"Focused the existing window of '{}'.", match->processName));
        return true;
    }
}
