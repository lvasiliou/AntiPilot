namespace AntiPilot.UI;

/// <summary>
/// One-time WinForms set-up, for the paths that did not expect to need any.
///
/// The key-press path deliberately creates no window, so it never used to initialise WinForms at
/// all. The palette and the failure balloon changed that: both can appear from a bare key press, and
/// without this they would come up unthemed and at the wrong DPI. It has to run before the first
/// control exists — SetCompatibleTextRenderingDefault throws once one does — so every entry point
/// that might show something goes through here.
/// </summary>
internal static class WinFormsHost
{
    private static bool _initialised;

    public static void Ensure()
    {
        if (_initialised)
        {
            return;
        }

        _initialised = true;

        try
        {
            ApplicationConfiguration.Initialize();
            Theme.Apply();
        }
        catch (InvalidOperationException ex)
        {
            // A control already existed, so someone got here first. Nothing to do and nothing worth
            // failing an action over.
            Log.Write($"WinForms was already initialised: {ex.Message}");
        }
    }
}
