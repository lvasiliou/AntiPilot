#pragma once

namespace AntiPilot::Shell::Startup
{
    // The tray icon at sign-in, as a shortcut in the user's Startup folder. A port of
    // TrayStartup.cs; see there for why it is a shortcut and not a StartupTask.
    enum class Availability
    {
        // No package identity, so no tray entry point to point a shortcut at.
        Unavailable,
        On,
        Off,
        // Switched off in Task Manager; only the user can undo that, in the same place.
        BlockedByUser,
    };

    Availability GetState();
    Availability Enable();
    Availability Disable();

    // The tray icon lives in AntiPilot.exe. Flipping the switch should produce or remove the icon
    // now, not at the next sign-in, so these reach across to that process.
    bool IsTrayRunning();
    bool StartTray();
    void StopTray();
}
