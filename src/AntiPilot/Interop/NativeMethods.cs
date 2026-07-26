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

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

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

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(nint hWnd);

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
