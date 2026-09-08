#include "Tap.h"

#include "Log.h"

#include <Windows.h>

#include <format>

namespace AntiPilot::Tap
{
    namespace
    {
        constexpr const wchar_t* PrimaryMutexName = L"Local\\AntiPilot.TapPrimary";
        constexpr const wchar_t* SecondPressEventName = L"Local\\AntiPilot.SecondTap";

        struct Handle
        {
            HANDLE value = nullptr;

            ~Handle()
            {
                if (value != nullptr)
                {
                    CloseHandle(value);
                }
            }
        };
    }

    Press Classify(int windowMs)
    {
        Handle primary;
        primary.value = CreateMutexW(nullptr, FALSE, PrimaryMutexName);
        if (primary.value == nullptr)
        {
            // Never let the detector be the reason a key press does nothing.
            Log::Write(std::format(L"Double-press detection failed (error {}); treating this as a single press.", GetLastError()));
            return Press::Single;
        }

        // The previous press dying mid-window leaves the mutex abandoned. It is ours all the same.
        DWORD wait = WaitForSingleObject(primary.value, 0);
        bool held = wait == WAIT_OBJECT_0 || wait == WAIT_ABANDONED;

        Handle signal;
        signal.value = CreateEventW(nullptr, FALSE, FALSE, SecondPressEventName);
        if (signal.value == nullptr)
        {
            if (held)
            {
                ReleaseMutex(primary.value);
            }

            Log::Write(std::format(L"Double-press detection failed (error {}); treating this as a single press.", GetLastError()));
            return Press::Single;
        }

        if (!held)
        {
            SetEvent(signal.value);
            Log::Write(L"Second press: handed to the press already waiting.");
            return Press::Handled;
        }

        // A press that arrived just after the last window closed leaves the event set. Clearing it
        // here stops that stale signal being read as a double.
        ResetEvent(signal.value);

        bool second = WaitForSingleObject(signal.value, static_cast<DWORD>(windowMs)) == WAIT_OBJECT_0;
        Log::Write(second
            ? L"Second press arrived inside the window: double press."
            : std::format(L"No second press within {} ms: single press.", windowMs));

        ReleaseMutex(primary.value);
        return second ? Press::Double : Press::Single;
    }
}
