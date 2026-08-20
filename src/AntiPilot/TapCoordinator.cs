namespace AntiPilot;

/// <summary>
/// Tells a single press from two quick ones.
///
/// Windows starts a fresh process for every press of the key, so the two presses of a double never
/// meet inside one process — the whole trick is two named kernel objects. The first press takes the
/// mutex and waits on the event for the double-tap window; a second press finds the mutex already
/// held, signals the event and exits without doing anything itself. The first press then runs the
/// double action instead of the single one.
///
/// This is why the feature is opt-in: the wait is unconditional, so every single press is delayed by
/// the width of the window whether or not a second press ever comes. That is the same trade every
/// double-click detector makes, and it cannot be avoided from outside the keyboard stack.
/// </summary>
internal static class TapCoordinator
{
    private const string PrimaryMutexName = @"Local\AntiPilot.TapPrimary";
    private const string SecondPressEventName = @"Local\AntiPilot.SecondTap";

    public enum Press
    {
        /// <summary>No second press arrived: run the single-press action.</summary>
        Single,

        /// <summary>A second press arrived inside the window: run the double-press action.</summary>
        Double,

        /// <summary>This process *is* the second press. Another one is acting on it; do nothing.</summary>
        Handled,
    }

    /// <summary>
    /// Classifies this press, blocking for up to <paramref name="windowMs"/> when it turns out to be
    /// the first of a possible pair.
    /// </summary>
    public static Press Classify(int windowMs)
    {
        Mutex? primary = null;
        bool held = false;

        try
        {
            primary = new Mutex(initiallyOwned: false, PrimaryMutexName);

            try
            {
                held = primary.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous press died mid-window. The mutex is ours all the same.
                held = true;
            }

            if (!held)
            {
                using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SecondPressEventName);
                signal.Set();
                Log.Write("Second press: handed to the press already waiting.");
                return Press.Handled;
            }

            using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SecondPressEventName))
            {
                // A press that arrived just after the last window closed leaves the event set.
                // Clearing it here stops that stale signal being read as a double.
                signal.Reset();

                bool second = signal.WaitOne(windowMs);
                Log.Write(second
                    ? "Second press arrived inside the window: double press."
                    : $"No second press within {windowMs} ms: single press.");

                return second ? Press.Double : Press.Single;
            }
        }
        catch (Exception ex)
        {
            // Never let the detector be the reason a key press does nothing.
            Log.Write($"Double-press detection failed ({ex.Message}); treating this as a single press.");
            return Press.Single;
        }
        finally
        {
            if (primary is not null)
            {
                if (held)
                {
                    try
                    {
                        primary.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Not ours any more; nothing to release.
                    }
                }

                primary.Dispose();
            }
        }
    }
}
