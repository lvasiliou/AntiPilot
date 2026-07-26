using System.Diagnostics;
using AntiPilot.Interop;

namespace AntiPilot;

public static class ActionRunner
{
    /// <summary>Carries out a configured action. Returns false when nothing was done.</summary>
    public static bool Run(KeyAction action)
    {
        if (!action.IsConfigured)
        {
            return false;
        }

        try
        {
            switch (action.Kind)
            {
                case ActionKind.MenuKey:
                    InputSender.SendMenuKey();
                    return true;

                case ActionKind.ShellApp:
                    InputSender.ReleaseStuckModifiers();
                    AppActivation.LaunchAppsFolderItem(action.Aumid!);
                    return true;

                case ActionKind.File:
                    InputSender.ReleaseStuckModifiers();
                    LaunchFile(action);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            // Worth a dialog even on the key-press path: a silent failure just looks like a dead key.
            Log.Write($"Action '{action.Kind}' failed: {ex}");
            MessageBox.Show(
                $"AntiPilot could not run the configured action.\r\n\r\n{ex.Message}",
                "AntiPilot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return false;
        }
    }

    private static void LaunchFile(KeyAction action)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ExpandEnvironmentVariables(action.Path!),
            UseShellExecute = true,
        };

        if (!string.IsNullOrWhiteSpace(action.Arguments))
        {
            startInfo.Arguments = Environment.ExpandEnvironmentVariables(action.Arguments);
        }

        if (!string.IsNullOrWhiteSpace(action.WorkingDirectory))
        {
            startInfo.WorkingDirectory = Environment.ExpandEnvironmentVariables(action.WorkingDirectory);
        }
        else if (Path.IsPathRooted(startInfo.FileName))
        {
            var dir = Path.GetDirectoryName(startInfo.FileName);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                startInfo.WorkingDirectory = dir;
            }
        }

        Log.Write($"Launching '{startInfo.FileName}' {startInfo.Arguments}");
        Process.Start(startInfo)?.Dispose();
    }
}
