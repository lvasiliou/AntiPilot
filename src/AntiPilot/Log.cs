namespace AntiPilot;

/// <summary>
/// Tiny append-only log. The key-press path runs with no UI, so this is the only
/// way to find out afterwards why nothing happened.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string LogPath => Path.Combine(AppConfig.ConfigDirectory, "antipilot.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppConfig.ConfigDirectory);

                // Keep it from growing forever.
                var file = new FileInfo(LogPath);
                if (file.Exists && file.Length > 256 * 1024)
                {
                    file.Delete();
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}";

                // Several AntiPilot processes can be alive at once (tray, settings, one per key
                // press), so an append can lose the race for the file. Give it a couple of tries.
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        using var stream = new FileStream(
                            LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                        using var writer = new StreamWriter(stream);
                        writer.Write(line);
                        return;
                    }
                    catch (IOException) when (attempt < 4)
                    {
                        Thread.Sleep(20);
                    }
                }
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
