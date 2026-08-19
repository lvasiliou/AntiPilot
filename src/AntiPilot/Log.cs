namespace AntiPilot;

/// <summary>
/// Tiny append-only log. The key-press path runs with no UI, so this is the only
/// way to find out afterwards why nothing happened.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();

    /// <summary>
    /// Serialises writes across processes, not just threads.
    ///
    /// Several AntiPilot processes are alive at once by design — the tray, the settings window, one
    /// per key press, and two at a time for every double press — and appends from different
    /// processes interleave inside a line, which was observed shredding entries in exactly the
    /// double-press case. The in-process lock below cannot see any of that.
    /// </summary>
    private static readonly Mutex FileGate = new(false, @"Local\AntiPilot.Log");

    private const long MaxBytes = 256 * 1024;

    public static string LogPath => Path.Combine(AppConfig.ConfigDirectory, "antipilot.log");

    /// <summary>The previous log. Kept so a rotation does not throw away the evidence.</summary>
    public static string PreviousLogPath => LogPath + ".1";

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}";
                var bytes = System.Text.Encoding.UTF8.GetBytes(line);

                bool held = false;
                try
                {
                    try
                    {
                        // A press that died mid-write leaves the mutex abandoned, which still means
                        // it is ours now. A wait that times out is not worth stalling a key press
                        // over, so the line is simply dropped.
                        held = FileGate.WaitOne(TimeSpan.FromSeconds(2));
                    }
                    catch (AbandonedMutexException)
                    {
                        held = true;
                    }

                    if (!held)
                    {
                        return;
                    }

                    Directory.CreateDirectory(AppConfig.ConfigDirectory);
                    RotateIfLarge();

                    // One Write of the whole line, under the mutex, so no other process can land
                    // between the timestamp and the newline.
                    using var stream = new FileStream(
                        LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    stream.Write(bytes, 0, bytes.Length);
                }
                finally
                {
                    if (held)
                    {
                        FileGate.ReleaseMutex();
                    }
                }
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    /// <summary>
    /// Moves the log aside once it gets big rather than deleting it. Whatever explains the problem
    /// being investigated is usually the part that has just scrolled past the size limit.
    /// </summary>
    private static void RotateIfLarge()
    {
        try
        {
            var file = new FileInfo(LogPath);
            if (!file.Exists || file.Length <= MaxBytes)
            {
                return;
            }

            File.Move(LogPath, PreviousLogPath, overwrite: true);
        }
        catch (IOException)
        {
            // Another process has it open. It will be rotated by whoever writes next.
        }
        catch (UnauthorizedAccessException)
        {
            // Same again; not worth failing a log write over.
        }
    }
}
