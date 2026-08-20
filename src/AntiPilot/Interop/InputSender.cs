using System.Runtime.InteropServices;
using static AntiPilot.Interop.NativeMethods;

namespace AntiPilot.Interop;

internal static class InputSender
{
    /// <summary>
    /// Synthesises a press of the Menu / context-menu key (VK_APPS) into whatever has focus.
    /// </summary>
    public static void SendMenuKey() => SendHotkey(new HotkeyDefinition(VK_APPS));

    /// <summary>
    /// Synthesises a chord: modifiers down, key down, key up, modifiers up. Order matters — a
    /// window that reads the modifier state on key-down sees nothing if the modifiers arrive late.
    /// </summary>
    public static void SendHotkey(HotkeyDefinition hotkey)
    {
        ReleaseStuckModifiers();

        var events = new List<INPUT>(10);

        // Left-hand modifiers throughout: the generic VK_CONTROL and friends are fine for
        // GetKeyState but some applications test specifically for the sided codes.
        AddModifier(events, hotkey.Control, VK_LCONTROL, 0x1D, extended: false, down: true);
        AddModifier(events, hotkey.Alt, VK_LMENU, 0x38, extended: false, down: true);
        AddModifier(events, hotkey.Shift, VK_LSHIFT, 0x2A, extended: false, down: true);
        AddModifier(events, hotkey.Windows, VK_LWIN, 0x5B, extended: true, down: true);

        ushort scan = (ushort)MapVirtualKeyW((uint)hotkey.VirtualKey, MAPVK_VK_TO_VSC);
        uint keyFlags = hotkey.IsExtended ? KEYEVENTF_EXTENDEDKEY : 0;

        events.Add(KeyInput(hotkey.VirtualKey, scan, keyFlags));
        events.Add(KeyInput(hotkey.VirtualKey, scan, keyFlags | KEYEVENTF_KEYUP));

        // Released in the reverse order they went down, so the chord unwinds cleanly.
        AddModifier(events, hotkey.Windows, VK_LWIN, 0x5B, extended: true, down: false);
        AddModifier(events, hotkey.Shift, VK_LSHIFT, 0x2A, extended: false, down: false);
        AddModifier(events, hotkey.Alt, VK_LMENU, 0x38, extended: false, down: false);
        AddModifier(events, hotkey.Control, VK_LCONTROL, 0x1D, extended: false, down: false);

        var inputs = events.ToArray();
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());

        if (sent != inputs.Length)
        {
            Log.Write($"SendInput sent {sent}/{inputs.Length} events for {hotkey.Format()}, " +
                      $"error {Marshal.GetLastWin32Error()}. The focused window is probably running " +
                      "elevated (UIPI blocks input from a normal process).");
            return;
        }

        var target = GetForegroundWindow();
        Log.Write(target == 0
            ? $"Sent {hotkey.Format()}, but no window has focus (locked session?), so nothing will react."
            : $"Sent {hotkey.Format()} to window 0x{target:X}.");
    }

    private static void AddModifier(List<INPUT> events, bool wanted, int virtualKey, ushort scan, bool extended, bool down)
    {
        if (!wanted)
        {
            return;
        }

        uint flags = extended ? KEYEVENTF_EXTENDEDKEY : 0;
        if (!down)
        {
            flags |= KEYEVENTF_KEYUP;
        }

        events.Add(KeyInput(virtualKey, scan, flags));
    }

    /// <summary>
    /// The physical Copilot key is a Shift+Win+F23 chord under the hood. If any of those
    /// are still logically down when we run, whatever we do next gets mangled, so drop them first.
    /// </summary>
    public static void ReleaseStuckModifiers()
    {
        var events = new List<INPUT>();

        bool winDown = IsDown(VK_LWIN) || IsDown(VK_RWIN);
        if (winDown)
        {
            // Releasing Win on its own pops the Start menu. Tapping a harmless key while it is
            // still held makes Windows treat it as a chord and swallow the Start menu.
            events.Add(KeyInput(VK_CONTROL, 0x1D, 0));
            events.Add(KeyInput(VK_CONTROL, 0x1D, KEYEVENTF_KEYUP));
        }

        AddReleaseIfDown(events, VK_LWIN, 0x5B, extended: true);
        AddReleaseIfDown(events, VK_RWIN, 0x5C, extended: true);
        AddReleaseIfDown(events, VK_LSHIFT, 0x2A, extended: false);
        AddReleaseIfDown(events, VK_RSHIFT, 0x36, extended: false);
        AddReleaseIfDown(events, VK_LCONTROL, 0x1D, extended: false);
        AddReleaseIfDown(events, VK_RCONTROL, 0x1D, extended: true);
        AddReleaseIfDown(events, VK_LMENU, 0x38, extended: false);
        AddReleaseIfDown(events, VK_RMENU, 0x38, extended: true);

        if (events.Count > 0)
        {
            var array = events.ToArray();
            SendInput((uint)array.Length, array, Marshal.SizeOf<INPUT>());
        }
    }

    private static void AddReleaseIfDown(List<INPUT> events, int vk, ushort scan, bool extended)
    {
        if (!IsDown(vk))
        {
            return;
        }

        events.Add(KeyInput(vk, scan, KEYEVENTF_KEYUP | (extended ? KEYEVENTF_EXTENDEDKEY : 0)));
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static INPUT KeyInput(int vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = 0,
            },
        },
    };
}
