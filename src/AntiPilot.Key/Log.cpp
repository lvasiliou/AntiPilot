#include "Log.h"

#include "Paths.h"
#include "Text.h"

#include <Windows.h>

#include <format>

namespace AntiPilot::Log
{
    namespace
    {
        /// <summary>Shared with the .NET processes, which write the same file under the same name.</summary>
        constexpr const wchar_t* GateName = L"Local\\AntiPilot.Log";

        constexpr LONGLONG MaxBytes = 256 * 1024;

        /// <summary>
        /// Moves the log aside once it gets big rather than deleting it. Whatever explains the
        /// problem being investigated is usually the part that has just scrolled past the limit.
        /// </summary>
        void RotateIfLarge(const std::wstring& path)
        {
            WIN32_FILE_ATTRIBUTE_DATA info{};
            if (!GetFileAttributesExW(path.c_str(), GetFileExInfoStandard, &info))
            {
                return;
            }

            LARGE_INTEGER size;
            size.HighPart = static_cast<LONG>(info.nFileSizeHigh);
            size.LowPart = info.nFileSizeLow;
            if (size.QuadPart <= MaxBytes)
            {
                return;
            }

            // Another process holding it open makes this fail; whoever writes next rotates it instead.
            MoveFileExW(path.c_str(), PreviousPath().c_str(), MOVEFILE_REPLACE_EXISTING);
        }

        /// <summary>Holds the cross-process gate for the lifetime of one write.</summary>
        class Gate
        {
        public:
            Gate()
            {
                _mutex = CreateMutexW(nullptr, FALSE, GateName);
                if (_mutex == nullptr)
                {
                    return;
                }

                // A press that died mid-write leaves the mutex abandoned, which still means it is
                // ours now. A wait that times out is not worth stalling a key press over.
                DWORD wait = WaitForSingleObject(_mutex, 2000);
                _held = wait == WAIT_OBJECT_0 || wait == WAIT_ABANDONED;
            }

            ~Gate()
            {
                if (_held)
                {
                    ReleaseMutex(_mutex);
                }

                if (_mutex != nullptr)
                {
                    CloseHandle(_mutex);
                }
            }

            Gate(const Gate&) = delete;
            Gate& operator=(const Gate&) = delete;

            bool Held() const { return _held; }

        private:
            HANDLE _mutex = nullptr;
            bool _held = false;
        };
    }

    std::wstring Path()
    {
        return Paths::ConfigDirectory() + L"\\antipilot.log";
    }

    std::wstring PreviousPath()
    {
        return Path() + L".1";
    }

    void Write(std::wstring_view message)
    {
        SYSTEMTIME now;
        GetLocalTime(&now);

        // Same shape as the .NET side's line, so the one file reads as one log.
        std::string line = Text::ToUtf8(std::format(
            L"{:04}-{:02}-{:02} {:02}:{:02}:{:02}.{:03}  {}\r\n",
            now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond, now.wMilliseconds,
            message));

        Gate gate;
        if (!gate.Held())
        {
            return;
        }

        std::wstring directory = Paths::ConfigDirectory();
        if (!Paths::EnsureDirectory(directory))
        {
            return;
        }

        std::wstring path = Path();
        RotateIfLarge(path);

        HANDLE file = CreateFileW(
            path.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr,
            OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return;
        }

        // One write of the whole line, under the mutex, so no other process can land between the
        // timestamp and the newline.
        DWORD written = 0;
        WriteFile(file, line.data(), static_cast<DWORD>(line.size()), &written, nullptr);
        CloseHandle(file);
    }
}
