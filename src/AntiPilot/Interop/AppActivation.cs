using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AntiPilot.Interop;

[Flags]
internal enum ActivateOptions
{
    None = 0x00000000,
    DesignMode = 0x00000001,
    NoErrorUI = 0x00000002,
    NoSplashScreen = 0x00000004,
}

[ComImport]
[Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IApplicationActivationManager
{
    int ActivateApplication(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
        ActivateOptions options,
        out uint processId);

    int ActivateForFile(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        nint itemArray,
        [MarshalAs(UnmanagedType.LPWStr)] string? verb,
        out uint processId);

    int ActivateForProtocol(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        nint itemArray,
        out uint processId);
}

[ComImport]
[Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
internal class ApplicationActivationManager
{
}

internal static class AppActivation
{
    /// <summary>
    /// Launches an entry of the Apps folder. Packaged apps (AUMIDs, which contain '!') go through
    /// the activation manager; everything else is handed to the shell as a "shell:AppsFolder\..." path.
    /// </summary>
    public static void LaunchAppsFolderItem(string parsingName)
    {
        if (parsingName.Contains('!'))
        {
            try
            {
                var manager = (IApplicationActivationManager)new ApplicationActivationManager();
                int hr = manager.ActivateApplication(parsingName, null, ActivateOptions.None, out uint pid);
                if (hr >= 0)
                {
                    Log.Write($"Activated '{parsingName}' (pid {pid}).");
                    return;
                }

                Log.Write($"ActivateApplication('{parsingName}') failed with 0x{hr:X8}; falling back to the shell.");
            }
            catch (Exception ex)
            {
                Log.Write($"ActivateApplication('{parsingName}') threw: {ex.Message}; falling back to the shell.");
            }
        }

        // Works for both packaged and classic Start-menu entries.
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"shell:AppsFolder\\{parsingName}",
            UseShellExecute = true,
        })?.Dispose();
    }
}
