#pragma once

namespace AntiPilot::Tap
{
    enum class Press
    {
        /// <summary>No second press arrived: run the single-press action.</summary>
        Single,

        /// <summary>A second press arrived inside the window: run the double-press action.</summary>
        Double,

        /// <summary>This process *is* the second press. Another one is acting on it; do nothing.</summary>
        Handled,
    };

    /// <summary>
    /// Tells a single press from two quick ones.
    ///
    /// Windows starts a fresh process for every press of the key, so the two presses of a double
    /// never meet inside one process — the whole trick is two named kernel objects. The first press
    /// takes the mutex and waits on the event for the double-tap window; a second press finds the
    /// mutex already held, signals the event and exits without doing anything itself. The first
    /// press then runs the double action instead of the single one.
    ///
    /// The names are the ones the .NET side used, so a mixed pair of processes still pairs up.
    /// Blocks for up to <paramref name="windowMs"/> when this turns out to be the first of a
    /// possible pair.
    /// </summary>
    Press Classify(int windowMs);
}
