#include "Input.h"

#include "Log.h"

#include <Windows.h>

#include <format>
#include <vector>

namespace AntiPilot::Input
{
    namespace
    {
        INPUT KeyInput(int virtualKey, WORD scan, DWORD flags)
        {
            INPUT input{};
            input.type = INPUT_KEYBOARD;
            input.ki.wVk = static_cast<WORD>(virtualKey);
            input.ki.wScan = scan;
            input.ki.dwFlags = flags;
            return input;
        }

        void AddModifier(std::vector<INPUT>& events, bool wanted, int virtualKey, WORD scan, bool extended, bool down)
        {
            if (!wanted)
            {
                return;
            }

            DWORD flags = extended ? KEYEVENTF_EXTENDEDKEY : 0;
            if (!down)
            {
                flags |= KEYEVENTF_KEYUP;
            }

            events.push_back(KeyInput(virtualKey, scan, flags));
        }

        bool IsDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        void AddReleaseIfDown(std::vector<INPUT>& events, int virtualKey, WORD scan, bool extended)
        {
            if (!IsDown(virtualKey))
            {
                return;
            }

            events.push_back(KeyInput(virtualKey, scan, KEYEVENTF_KEYUP | (extended ? KEYEVENTF_EXTENDEDKEY : 0)));
        }

        UINT Send(std::vector<INPUT>& events)
        {
            return events.empty() ? 0 : SendInput(static_cast<UINT>(events.size()), events.data(), sizeof(INPUT));
        }
    }

    void SendMenuKey()
    {
        HotkeyDefinition menu;
        menu.virtualKey = VK_APPS;
        SendHotkey(menu);
    }

    void ReleaseStuckModifiers()
    {
        std::vector<INPUT> events;

        if (IsDown(VK_LWIN) || IsDown(VK_RWIN))
        {
            // Releasing Win on its own pops the Start menu. Tapping a harmless key while it is
            // still held makes Windows treat it as a chord and swallow the Start menu.
            events.push_back(KeyInput(VK_CONTROL, 0x1D, 0));
            events.push_back(KeyInput(VK_CONTROL, 0x1D, KEYEVENTF_KEYUP));
        }

        AddReleaseIfDown(events, VK_LWIN, 0x5B, true);
        AddReleaseIfDown(events, VK_RWIN, 0x5C, true);
        AddReleaseIfDown(events, VK_LSHIFT, 0x2A, false);
        AddReleaseIfDown(events, VK_RSHIFT, 0x36, false);
        AddReleaseIfDown(events, VK_LCONTROL, 0x1D, false);
        AddReleaseIfDown(events, VK_RCONTROL, 0x1D, true);
        AddReleaseIfDown(events, VK_LMENU, 0x38, false);
        AddReleaseIfDown(events, VK_RMENU, 0x38, true);

        Send(events);
    }

    void SendHotkey(const HotkeyDefinition& hotkey)
    {
        ReleaseStuckModifiers();

        std::vector<INPUT> events;
        events.reserve(10);

        // Left-hand modifiers throughout: the generic VK_CONTROL and friends are fine for
        // GetKeyState but some applications test specifically for the sided codes.
        AddModifier(events, hotkey.control, VK_LCONTROL, 0x1D, false, true);
        AddModifier(events, hotkey.alt, VK_LMENU, 0x38, false, true);
        AddModifier(events, hotkey.shift, VK_LSHIFT, 0x2A, false, true);
        AddModifier(events, hotkey.windows, VK_LWIN, 0x5B, true, true);

        WORD scan = static_cast<WORD>(MapVirtualKeyW(static_cast<UINT>(hotkey.virtualKey), MAPVK_VK_TO_VSC));
        DWORD keyFlags = hotkey.IsExtended() ? KEYEVENTF_EXTENDEDKEY : 0;

        events.push_back(KeyInput(hotkey.virtualKey, scan, keyFlags));
        events.push_back(KeyInput(hotkey.virtualKey, scan, keyFlags | KEYEVENTF_KEYUP));

        // Released in the reverse order they went down, so the chord unwinds cleanly.
        AddModifier(events, hotkey.windows, VK_LWIN, 0x5B, true, false);
        AddModifier(events, hotkey.shift, VK_LSHIFT, 0x2A, false, false);
        AddModifier(events, hotkey.alt, VK_LMENU, 0x38, false, false);
        AddModifier(events, hotkey.control, VK_LCONTROL, 0x1D, false, false);

        UINT sent = Send(events);
        if (sent != events.size())
        {
            Log::Write(std::format(
                L"SendInput sent {}/{} events for {}, error {}. The focused window is probably running "
                L"elevated (UIPI blocks input from a normal process).",
                sent, events.size(), hotkey.Format(), GetLastError()));
            return;
        }

        HWND target = GetForegroundWindow();
        Log::Write(target == nullptr
            ? std::format(L"Sent {}, but no window has focus (locked session?), so nothing will react.", hotkey.Format())
            : std::format(L"Sent {} to window 0x{:X}.", hotkey.Format(), reinterpret_cast<uintptr_t>(target)));
    }
}
