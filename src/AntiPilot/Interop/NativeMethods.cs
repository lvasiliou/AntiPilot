using System.Runtime.InteropServices;

namespace AntiPilot.Interop;

internal static unsafe partial class NativeMethods
{
    public const int VK_APPS = 0x5D;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_CONTROL = 0x11;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;

    public const int VK_MENU = 0x12;
    public const int VK_SHIFT = 0x10;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    public const uint MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    public const int SW_RESTORE = 9;
    public const int SW_MINIMIZE = 6;
    public const int SW_SHOW = 5;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial uint MapVirtualKeyW(uint uCode, uint uMapType);

    // ---- window and process plumbing, for launch-or-focus and per-app rules ------------------

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int GW_OWNER = 4;

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial int GetWindowTextLengthW(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial int GetWindowTextW(nint hWnd, char* lpString, int nMaxCount);

    /// <summary>Window caption, or an empty string when it has none.</summary>
    public static string GetWindowText(nint hWnd)
    {
        int length = GetWindowTextLengthW(hWnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length + 1];
        int copied;
        fixed (char* p = buffer)
        {
            copied = GetWindowTextW(hWnd, p, buffer.Length);
        }

        return copied <= 0 ? string.Empty : new string(buffer, 0, copied);
    }

    [LibraryImport("user32.dll")]
    public static partial nint GetWindow(nint hWnd, uint uCmd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    /// <summary>Return false to stop the enumeration.</summary>
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    // DllImport rather than LibraryImport: the source generator will not marshal a delegate, and a
    // function pointer would mean threading state through lParam by hand for no benefit here.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(nint hWndParent, EnumWindowsProc lpEnumFunc, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BringWindowToTop(nint hWnd);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    [LibraryImport("kernel32.dll", EntryPoint = "GetApplicationUserModelId")]
    private static partial int GetApplicationUserModelIdRaw(nint hProcess, uint* length, char* buffer);

    /// <summary>
    /// AUMID of another running process, or null when it has no package identity. This is how an
    /// already-running Store app is matched back to the AUMID stored in the config.
    /// </summary>
    public static string? GetApplicationUserModelId(uint processId)
    {
        nint handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            uint length = 0;
            const int ERROR_INSUFFICIENT_BUFFER = 122;
            if (GetApplicationUserModelIdRaw(handle, &length, null) != ERROR_INSUFFICIENT_BUFFER || length == 0)
            {
                return null;
            }

            var buffer = new char[length];
            int rc;
            fixed (char* p = buffer)
            {
                rc = GetApplicationUserModelIdRaw(handle, &length, p);
            }

            return rc == 0 ? new string(buffer, 0, (int)length).TrimEnd('\0') : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentPackageFamilyName")]
    private static partial int GetCurrentPackageFamilyNameRaw(uint* packageFamilyNameLength, char* packageFamilyName);

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentApplicationUserModelId")]
    private static partial int GetCurrentApplicationUserModelIdRaw(uint* length, char* buffer);

    /// <summary>
    /// AUMID of the manifest &lt;Application&gt; entry that was activated. The package has two
    /// entries pointing at this same exe, which is how a Start menu launch of "AntiPilot Settings"
    /// is told apart from the Copilot key launching "AntiPilot".
    /// </summary>
    public static string? GetCurrentApplicationUserModelId()
    {
        uint length = 0;
        int rc = GetCurrentApplicationUserModelIdRaw(&length, null);
        const int ERROR_INSUFFICIENT_BUFFER = 122;
        if (rc != ERROR_INSUFFICIENT_BUFFER || length == 0)
        {
            return null;
        }

        var buffer = new char[length];
        fixed (char* p = buffer)
        {
            rc = GetCurrentApplicationUserModelIdRaw(&length, p);
        }

        return rc == 0 ? new string(buffer, 0, (int)length).TrimEnd('\0') : null;
    }

    /// <summary>Package family name when running inside the MSIX package, otherwise null.</summary>
    public static string? GetCurrentPackageFamilyName()
    {
        uint length = 0;
        int rc = GetCurrentPackageFamilyNameRaw(&length, null);
        const int ERROR_INSUFFICIENT_BUFFER = 122;
        if (rc != ERROR_INSUFFICIENT_BUFFER || length == 0)
        {
            return null; // APPMODEL_ERROR_NO_PACKAGE (15700) when unpackaged.
        }

        var buffer = new char[length];
        fixed (char* p = buffer)
        {
            rc = GetCurrentPackageFamilyNameRaw(&length, p);
        }

        return rc == 0 ? new string(buffer, 0, (int)length).TrimEnd('\0') : null;
    }
}
