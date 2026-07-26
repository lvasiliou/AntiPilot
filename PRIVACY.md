# AntiPilot privacy policy

**Effective 26 July 2026**

AntiPilot does not collect, transmit or share any data. It has no accounts, no telemetry, no
analytics, no crash reporting, no advertising, and no third-party services. It makes no network
connections of any kind — there is no networking code in the app.

## What is stored, and where

Everything AntiPilot saves stays on your PC, in the app's own data folder:

`%LOCALAPPDATA%\Packages\<AntiPilot package family name>\LocalCache\Local\AntiPilot\`

| File            | Contents                                                                        |
| --------------- | ------------------------------------------------------------------------------- |
| `config.json`   | What you chose the key to do: the identifier of the app you picked, or the path, arguments and start-in folder you typed, plus a flag recording whether the tray icon has introduced itself |
| `antipilot.log` | A short diagnostic log — when the key was pressed, what was launched, and any errors. It contains the paths and app identifiers you configured |

Neither file is sent anywhere. Both are yours to read; the *Open log* link in the settings window
opens the log.

## What AntiPilot reads

To do its job it reads, locally and only on your machine:

- The list of apps in your Start menu, so it can offer them in the app picker.
- The Windows setting recording which app the Copilot key currently launches, so it can tell you
  whether it is pointed at AntiPilot.
- Your Windows light/dark theme preference.

None of this is recorded, copied off the device, or used for anything else.

## What AntiPilot changes

- Its own settings file, when you press Save.
- A shortcut in your Startup folder — only while "Show the tray icon, and start it when I sign in"
  is ticked, and removed when you untick it.

## About the "runFullTrust" capability

The package declares `runFullTrust`. That is the standard capability for a packaged desktop
application on Windows, and it is what allows AntiPilot to start the program you chose and to send
the Menu key to the window you are using. It is not used to gather information, and it does not
change anything written above.

## Apps that AntiPilot launches

When AntiPilot starts the app, file or link you chose, that program takes over and its own privacy
policy applies. AntiPilot passes it nothing beyond the arguments you typed yourself.

## Removing your data

Uninstalling AntiPilot removes the app and its data folder, including the settings and log files.
If the tray icon's sign-in shortcut was enabled, uninstalling with the supplied `uninstall.ps1`
removes it too; otherwise delete "AntiPilot tray icon" from Task Manager → Startup apps.

## Children

AntiPilot collects no data from anyone, including children.

## Changes

If this policy ever changes, the revised version will be published at the same address with a new
effective date.

## Contact

Questions about this policy: open an issue on the AntiPilot repository.
