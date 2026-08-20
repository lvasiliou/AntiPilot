using AntiPilot.Interop;

namespace AntiPilot;

/// <summary>
/// Checks that an action still points at something real.
///
/// A key that quietly does nothing is the worst failure this app has, and the usual cause is
/// mundane: the app was uninstalled, or the file moved. Catching it while the settings window is
/// open costs the user one dialog; not catching it costs them a dead key and a trip to the log.
/// </summary>
public static class ActionValidator
{
    /// <summary>A description of what is wrong, or null when the action looks fine.</summary>
    public static string? Validate(KeyAction action)
    {
        // "Nothing" is a choice, not an oversight, and is the state of every fresh install — so it
        // is the one kind that never complains. Every other kind having no target is worth saying:
        // picking "launch an app" and not picking the app produces a key that does nothing at all.
        switch (action.Kind)
        {
            case ActionKind.ShellApp:
                if (string.IsNullOrWhiteSpace(action.Aumid))
                {
                    return Strings.NoAppChosen;
                }

                return ShellApps.Exists(action.Aumid!) ? null : Strings.TargetMissingApp;

            case ActionKind.File:
                if (string.IsNullOrWhiteSpace(action.Path))
                {
                    return Strings.TargetMissingFile;
                }

                return FileTargetExists(action.Path!) ? null : Strings.TargetMissingFile;

            case ActionKind.Hotkey:
                return HotkeyDefinition.TryParse(action.Hotkey, out _) ? null : Strings.HotkeyInvalid;

            default:
                return null;
        }
    }

    private static bool FileTargetExists(string path)
    {
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path).Trim();

            if (expanded.Length == 0)
            {
                return false;
            }

            // A URL, a shell: location or an ms-settings: link has no file to look for, and the
            // shell is the only thing that can say whether it resolves. Take it on trust.
            if (Uri.TryCreate(expanded, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                return true;
            }

            if (File.Exists(expanded) || Directory.Exists(expanded))
            {
                return true;
            }

            // A bare command such as "notepad" or "winget" is resolved against PATH at launch, so
            // check the same way rather than calling it missing.
            return ResolvesOnPath(expanded);
        }
        catch (Exception ex)
        {
            Log.Write($"Could not check '{path}': {ex.Message}");
            return true;
        }
    }

    private static bool ResolvesOnPath(string command)
    {
        if (Path.IsPathRooted(command) || command.Contains(Path.DirectorySeparatorChar))
        {
            return false;
        }

        var directories = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';');

        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var extension in extensions)
            {
                try
                {
                    if (File.Exists(Path.Combine(directory.Trim(), command + extension.Trim())))
                    {
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry; skip it.
                }
            }
        }

        return false;
    }
}
