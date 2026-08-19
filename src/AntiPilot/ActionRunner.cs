using System.Diagnostics;
using AntiPilot.Interop;
using AntiPilot.UI;

namespace AntiPilot;

/// <summary>How a failure should reach the user.</summary>
public enum ActionFeedback
{
    /// <summary>A modal dialog. Right when a window of ours is already on screen.</summary>
    Dialog,

    /// <summary>
    /// A notification-area balloon. Right on the key-press path, where a modal dialog would steal
    /// focus from whatever the user was actually doing.
    /// </summary>
    Balloon,
}

public static class ActionRunner
{
    /// <summary>Carries out a configured action. Returns false when nothing was done.</summary>
    public static bool Run(KeyAction action, ActionFeedback feedback = ActionFeedback.Dialog, AppConfig? config = null)
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

                case ActionKind.Hotkey:
                    if (!HotkeyDefinition.TryParse(action.Hotkey, out var hotkey))
                    {
                        Log.Write($"Cannot parse the shortcut '{action.Hotkey}'.");
                        return false;
                    }

                    InputSender.SendHotkey(hotkey);
                    return true;

                case ActionKind.Palette:
                    return PaletteForm.Show(config ?? AppConfig.Load(), feedback);

                case ActionKind.ShellApp:
                    InputSender.ReleaseStuckModifiers();
                    return LaunchApp(action);

                case ActionKind.File:
                    InputSender.ReleaseStuckModifiers();
                    return LaunchFile(action);

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            // Worth telling the user even on the key-press path: a silent failure just looks like
            // a dead key.
            Log.Write($"Action '{action.Kind}' failed: {ex}");
            Notifier.ShowError(Strings.ActionFailedTitle, ex.Message, feedback);
            return false;
        }
    }

    private static bool LaunchApp(KeyAction action)
    {
        if (action.Behaviour != LaunchBehaviour.Always &&
            WindowFinder.TryFocus(action.Aumid, path: null, allowMinimise: action.Behaviour == LaunchBehaviour.Toggle))
        {
            return true;
        }

        AppActivation.LaunchAppsFolderItem(action.Aumid!);
        return true;
    }

    private static bool LaunchFile(KeyAction action)
    {
        var fileName = Environment.ExpandEnvironmentVariables(action.Path!);

        // Focusing only makes sense for a program. A folder or a URL has no window of its own that
        // could be brought forward, so those always go to the shell.
        if (action.Behaviour != LaunchBehaviour.Always &&
            fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            WindowFinder.TryFocus(aumid: null, path: fileName, allowMinimise: action.Behaviour == LaunchBehaviour.Toggle))
        {
            return true;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
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
            var directory = Path.GetDirectoryName(startInfo.FileName);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                startInfo.WorkingDirectory = directory;
            }
        }

        Log.Write($"Launching '{startInfo.FileName}' {startInfo.Arguments}");
        Process.Start(startInfo)?.Dispose();
        return true;
    }
}
