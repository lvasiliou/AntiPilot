using System.Diagnostics;
using static AntiPilot.Interop.NativeMethods;

namespace AntiPilot.Interop;

/// <summary>
/// Finds the window an already-running target owns, so the key can focus it instead of starting a
/// second copy, and works out which app is in front so per-app rules can be applied.
/// </summary>
internal static class WindowFinder
{
    /// <summary>
    /// UWP and packaged-desktop apps get their top-level window from this process rather than their
    /// own, so a bare process lookup reports the host for half the Store. Both paths below step
    /// through it to the window's real owner.
    /// </summary>
    private const string FrameHost = "ApplicationFrameHost";

    /// <summary>A window plus the process that really owns it.</summary>
    private readonly record struct TopLevelWindow(nint Handle, uint ProcessId, string? ProcessName, string? Aumid);

    /// <summary>
    /// Executable name (no extension) of the app in the foreground, or null when there is none.
    /// Used to pick a per-app rule.
    /// </summary>
    public static string? GetForegroundProcessName()
    {
        var window = GetForegroundWindow();
        if (window == 0)
        {
            return null;
        }

        return DescribeWindow(window).ProcessName;
    }

    /// <summary>
    /// Every app with a real window right now, one entry per process, so a per-app rule can be
    /// built by pointing at something already open rather than by guessing an executable name.
    /// </summary>
    public static List<(string ProcessName, string Title)> ListWindowedApps()
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        EnumWindows((handle, _) =>
        {
            if (!IsCandidateWindow(handle))
            {
                return true;
            }

            var window = DescribeWindow(handle);
            if (string.IsNullOrEmpty(window.ProcessName) || seen.ContainsKey(window.ProcessName!))
            {
                return true;
            }

            seen[window.ProcessName!] = NativeMethods.GetWindowText(handle);
            return true;
        }, 0);

        return seen
            .Select(pair => (ProcessName: pair.Key, Title: pair.Value))
            .OrderBy(entry => entry.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Brings the target's existing window to the front, or minimises it when it is already there
    /// and <paramref name="allowMinimise"/> is set. False means nothing matched and the caller
    /// should launch instead.
    /// </summary>
    public static bool TryFocus(string? aumid, string? path, bool allowMinimise)
    {
        var match = FindWindow(aumid, path);
        if (match is not { } window)
        {
            return false;
        }

        if (allowMinimise && GetForegroundWindow() == window.Handle && !IsIconic(window.Handle))
        {
            ShowWindow(window.Handle, SW_MINIMIZE);
            Log.Write($"Minimised the already-focused window of '{window.ProcessName}'.");
            return true;
        }

        Activate(window.Handle);
        Log.Write($"Focused the existing window of '{window.ProcessName}'.");
        return true;
    }

    /// <summary>
    /// Windows only lets the foreground process hand focus away, so SetForegroundWindow alone is
    /// unreliable from here. Attaching to the current foreground thread's input queue first is the
    /// long-standing way round it.
    /// </summary>
    private static void Activate(nint window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, SW_RESTORE);
        }

        var foreground = GetForegroundWindow();
        uint us = GetCurrentThreadId();
        uint them = foreground == 0 ? us : GetWindowThreadProcessId(foreground, out _);

        bool attached = them != us && AttachThreadInput(us, them, true);
        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(us, them, false);
            }
        }
    }

    private static TopLevelWindow? FindWindow(string? aumid, string? path)
    {
        string? wantedAumid = !string.IsNullOrWhiteSpace(aumid) && aumid!.Contains('!') ? aumid : null;
        string? wantedProcess = null;

        // A classic Start-menu entry parses to a path, and so does a File action; either way the
        // executable name is what a running window can be matched on.
        var candidate = wantedAumid is null ? (aumid ?? path) : path;
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(candidate!);
                if (expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    wantedProcess = Path.GetFileNameWithoutExtension(expanded);
                }
            }
            catch (ArgumentException)
            {
                // Not a usable path; nothing to match on.
            }
        }

        if (wantedAumid is null && wantedProcess is null)
        {
            return null;
        }

        TopLevelWindow? best = null;

        EnumWindows((handle, _) =>
        {
            if (!IsCandidateWindow(handle))
            {
                return true;
            }

            var window = DescribeWindow(handle);

            bool matches =
                (wantedAumid is not null && string.Equals(window.Aumid, wantedAumid, StringComparison.OrdinalIgnoreCase)) ||
                (wantedProcess is not null && string.Equals(window.ProcessName, wantedProcess, StringComparison.OrdinalIgnoreCase));

            if (!matches)
            {
                return true;
            }

            best = window;

            // Prefer whatever is already on screen over a minimised window of the same app.
            return IsIconic(handle);
        }, 0);

        return best;
    }

    /// <summary>Filters out the invisible, the owned and the tool windows nobody means by "the window".</summary>
    private static bool IsCandidateWindow(nint handle)
    {
        if (!IsWindowVisible(handle) || GetWindowTextLengthW(handle) == 0)
        {
            return false;
        }

        if (GetWindow(handle, GW_OWNER) != 0)
        {
            return false;
        }

        return ((int)GetWindowLongPtr(handle, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) == 0;
    }

    private static TopLevelWindow DescribeWindow(nint handle)
    {
        GetWindowThreadProcessId(handle, out uint processId);
        var name = TryGetProcessName(processId);

        if (string.Equals(name, FrameHost, StringComparison.OrdinalIgnoreCase))
        {
            uint hosted = FindHostedProcess(handle, processId);
            if (hosted != 0)
            {
                processId = hosted;
                name = TryGetProcessName(processId);
            }
        }

        return new TopLevelWindow(handle, processId, name, NativeMethods.GetApplicationUserModelId(processId));
    }

    /// <summary>
    /// The real app behind an ApplicationFrameHost window: its CoreWindow child belongs to a
    /// different process, which is the one the user thinks of as the app.
    /// </summary>
    private static uint FindHostedProcess(nint frame, uint framePid)
    {
        uint found = 0;

        EnumChildWindows(frame, (child, _) =>
        {
            GetWindowThreadProcessId(child, out uint childPid);
            if (childPid != 0 && childPid != framePid)
            {
                found = childPid;
                return false;
            }

            return true;
        }, 0);

        return found;
    }

    private static string? TryGetProcessName(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception)
        {
            // Gone between the enumeration and here, or not ours to look at.
            return null;
        }
    }
}
