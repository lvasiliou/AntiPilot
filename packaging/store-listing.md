# Microsoft Store listing

Copy for the Partner Center listing fields. Character limits from Partner Center are noted so the
copy can be checked before pasting.

## Product identity  *(Partner Center → Product identity)*

| Field                              | Value                                                              |
| ---------------------------------- | ------------------------------------------------------------------ |
| Package/Identity/Name              | `5676LambrosVasiliou.AntiPilot`                                      |
| Package/Identity/Publisher         | `CN=E4150ECD-C5C0-4302-91B1-E90B7B7F602B`                            |
| Package/Properties/PublisherDisplayName | `Lambros Vasiliou`                                              |
| Package Family Name                | `5676LambrosVasiliou.AntiPilot_ry1r8aenh16n2`                        |
| Store ID                           | `9N4S7TXSMP3P`                                                       |

Build the matching package with `.\build.ps1 -Target Store -Version <x.y.z.0>`; it comes out
unsigned, which is what you upload.

---

## Product name  *(max 256)*

AntiPilot

## Short description  *(max 1000 — shown in search results)*

Make the Copilot key do something you actually want. Point it at any installed app, a program, file,
folder or link, a keyboard shortcut of your choice, or turn it back into the Menu key that keyboards
dropped. Two quick presses can do something different from one, and the key can change its job
depending on which app is in front. Nothing runs in the background: a press starts AntiPilot, does
the one thing, and exits.

> Store search truncates this to roughly the first 100-150 characters, so the promise has to land in
> the first sentence. The previous version opened by explaining a Windows shortcoming — two thirds
> of the visible text was spent on Microsoft's picker before saying what the app does. The mechanism
> is still worth explaining; it belongs in the description below, where it now is.

## Description  *(max 10,000)*

**Your Copilot key. Your app.**

Windows 11 will let you customise the Copilot key, but only to an app that is MSIX packaged, signed,
and registered as a Copilot hardware key provider. Almost no app bothers, which is why the picker
under Settings → Bluetooth & devices → Keyboard is usually empty apart from Copilot itself.

AntiPilot does bother. Choose it once in Settings, then decide what the key does:

**Launch an installed app**
Anything in the Start menu's app list, Microsoft Store apps included. Pick it from a searchable list
with real icons — no hunting for install paths. If the app is already open, AntiPilot can bring that
window to the front instead of starting a second copy, or minimise it if you are already looking at
it — so one key becomes a way to flick in and out of the thing you use most.

**Launch a program, file, folder or link**
Any path or URL, with optional arguments and a start-in folder. Environment variables like
%USERPROFILE% are expanded.

**Send a keyboard shortcut**
Any combination, into whatever you are using: Ctrl+Shift+Esc for Task Manager, Win+V for clipboard
history, Print Screen, the media keys, or F13 to F24 for macro software that listens for keys no
keyboard has. Press the shortcut to capture it, or take one of the presets.

**Act as the Menu key**
The context-menu key that vanished from modern keyboards. AntiPilot sends the real thing, so the
context menu opens for whatever has focus — exactly like a right-click.

**Open a quick-launch palette**
A small, keyboard-first list of the things you reach for. Type to filter, press 1 to 9 to run an
entry outright, Esc to dismiss. The action runs after the palette closes, so a shortcut lands on the
window you were actually using.

**One key, more than one job**

Two quick presses can run a different action from a single press — a second thing on the same key,
for the cost of a short wait while AntiPilot decides which you meant. It is off by default and the
wait is adjustable, because that delay is real and you should choose to accept it.

Rules can also change what the key does depending on which app is in front: the Menu key inside your
editor, a shortcut inside your browser, your usual app everywhere else.

**Out of your way**

A key press starts AntiPilot, does the one thing, and exits. Nothing sits in memory waiting for a
keystroke, and no window appears unless you have not set anything up yet. If you would rather have
it in reach, one checkbox puts an icon in the notification area and brings it back at sign-in — off
by default.

The settings window is built to match Windows 11 — navigation down the left, settings cards, and
your own accent colour throughout. It follows light and dark as you switch, tells you at a glance
whether the key is currently pointed at AntiPilot, and takes you straight to the Windows page where
that is set. Your setup can be exported to a file and imported on another PC.

**In your language**

English, Russian, Spanish, Simplified and Traditional Chinese, Brazilian Portuguese, Turkish,
Japanese, Korean, Arabic, Indonesian and Greek. It follows your Windows language by default, and
Arabic mirrors the whole window rather than just translating it.

**Works with Win+C too**

No Copilot key on your keyboard? Windows sends the same signal for Win+C, so AntiPilot answers that
as well.

**Open source**

MIT licensed. The full licence text ships in the app under About.

**Requirements**

- Windows 11, build 22621 or newer
- Selecting AntiPilot once under Settings → Bluetooth & devices → Keyboard → Shortcuts and hotkeys
  → Customize Copilot key on keyboard → Custom

**Good to know**

- Windows blocks simulated keystrokes aimed at windows running as administrator, so the Menu key
  action does nothing while an elevated window is in front. That is a Windows security rule, not
  something an app can opt out of.
- AntiPilot changes only what the Copilot key launches. It does not remap other keys, and it does
  not remove Copilot from your PC.

## Product features  *(up to 20, max 200 each — shown as bullets)*

1. Point the Copilot key at any installed app, including Microsoft Store apps
2. Or at any program, file, folder or link, with arguments and a start-in folder
3. Or send any keyboard shortcut — Ctrl+Shift+Esc, Win+V, Print Screen, media keys, F13 to F24
4. Or turn the key back into the Menu key, for the context menu of whatever has focus
5. Or open a quick-launch palette: type to filter, press 1 to 9 to run an entry, Esc to dismiss
6. Give two quick presses their own action, separate from a single press
7. Change what the key does depending on which app is in front
8. Bring an app you already have open to the front instead of starting a second copy
9. Answers Win+C as well, for keyboards without a Copilot key
10. Nothing runs in the background — the key press starts it, it acts, it exits
11. Optional notification-area icon, off by default, with a sign-in switch
12. A settings window built to match Windows 11, in your own accent colour
13. Follows the Windows light and dark theme, and keeps up when you switch
14. Available in 12 languages, following your Windows language by default
15. Searchable app picker with real Windows icons
16. Export and import your setup, to move it between PCs
17. Shows whether the Copilot key currently points at AntiPilot, and links straight to that setting
18. Open source, MIT licensed

## Keywords / search terms  *(up to 7, max 40 characters each, and no more than 21 words in total)*

Store listings → language → **Additional information** → *Keywords* (called *Search terms* in the
older UI). Never shown to customers; they only feed Store search. These seven use 17 of the 21 words.

- copilot key
- remap copilot key
- menu key
- context menu key
- keyboard shortcut
- disable copilot
- copilot key launcher

## What's new in this version  *(max 1500)*

One key, more than one job.

• Send any keyboard shortcut — Ctrl+Shift+Esc, Win+V, Print Screen, the media keys, or F13 to F24
for macro software. Press the shortcut to capture it, or take a preset.
• Give two quick presses their own action, separate from a single press.
• Change what the key does depending on which app is in front.
• Open a quick-launch palette: type to filter, press 1 to 9 to run an entry, Esc to dismiss.
• Bring an app you already have open to the front instead of starting another copy — or minimise it
if you are already looking at it.

A new settings window, built to match Windows 11: navigation down the left, settings cards, and your
own accent colour throughout. It follows light and dark as you switch them.

Now in 12 languages — English, Russian, Spanish, Simplified and Traditional Chinese, Brazilian
Portuguese, Turkish, Japanese, Korean, Arabic, Indonesian and Greek — following your Windows
language by default. Arabic mirrors the layout.

Smaller things: export and import your setup to move it between PCs; a failed action now tells you
with a notification instead of a dialog that steals your focus; and every key press starts a little
faster.

## Copyright and trademark info  *(max 200 — this is 178)*

Copyright (c) 2026 Lambros Vasiliou. Windows, Windows 11 and Microsoft Copilot are trademarks of Microsoft Corporation. AntiPilot is not affiliated with or endorsed by Microsoft.

Keep the disclaimer: the listing names Copilot repeatedly, so saying plainly that this is not a
Microsoft product removes the obvious certification objection.

## Developed by  *(max 255 — leave blank)*

Blank. Windows always shows **Published by** with your publisher display name ("Lambros Vasiliou"),
whether or not this field is filled, so filling it in just prints the same name twice.

## Privacy policy URL  *(required — Partner Center asks for it because the package declares runFullTrust)*

`<paste the public URL of PRIVACY.md here>`

The text lives in [PRIVACY.md](../PRIVACY.md) at the root of this repository. It has to be reachable
at a public URL before the submission passes; see "Publishing the privacy policy" in the README.

**Do not fight the personal-information declaration.** Partner Center derives it from the declared
capabilities, and `runFullTrust` forces it on — setting it to *No* silently flips back. That
capability is not optional: `EntryPoint="Windows.FullTrustApplication"` requires it, so every
packaged desktop app declares it, and the package declares nothing else. Leave the answer as it
lands and supply the privacy policy URL, which is all the requirement actually asks for. The policy
itself states plainly that nothing is collected, so the two are not in conflict: the declaration
describes what the capability *could* reach, not what the app does.

## Notes for certification  *(paste into "Notes for certification" — covers the restricted capability)*

**Why `runFullTrust` is declared.** A Copilot hardware key provider must be an MSIX-packaged app
that declares the `com.microsoft.windows.copilotkeyprovider` app extension, and AntiPilot's whole
function — handing the key press on to a Win32 app, a file, or the keyboard — requires desktop APIs.
It therefore uses `EntryPoint="Windows.FullTrustApplication"`, which requires `runFullTrust`. It is
the only capability the package declares, and the app runs `asInvoker`: it never requests elevation.

**Everything that capability is used for.** In full, and in the order a user meets them:

1. **Starting what the user chose.** `IApplicationActivationManager::ActivateApplication` for
   packaged apps, `ShellExecute` (via `Process.Start`) for a program, file, folder or link.
2. **Listing installed apps** for the picker, and drawing their icons: the `Shell.Application` COM
   object over `shell:AppsFolder`, and `SHCreateItemFromParsingName` /
   `IShellItemImageFactory`. Read-only.
3. **Synthesising keystrokes** with `SendInput`, when the user has chosen the Menu key action or a
   keyboard shortcut of their own. `GetAsyncKeyState` and `MapVirtualKeyW` support this — the first
   to release modifier keys the physical Copilot key leaves held, the second for correct scan codes.
   The app installs no keyboard hook and never observes typing.
4. **Bringing an already-running window to the front**, when the user has asked for that instead of
   a second copy: `EnumWindows`, `GetWindowThreadProcessId`, `SetForegroundWindow`,
   `AttachThreadInput`, `ShowWindow`, and `GetApplicationUserModelId` on a process handle opened
   with `PROCESS_QUERY_LIMITED_INFORMATION`, to match a running Store app to the one chosen.
5. **Reading which app is in the foreground**, if — and only if — the user has created a per-app
   rule. `GetForegroundWindow` plus the process name, read at the instant the key is pressed, used
   to select which action to run and then discarded. It is not stored, aggregated or transmitted;
   it appears only in the local diagnostic log described below.
6. **Reading five HKCU settings**, all read-only: the current Copilot key target
   (`Shell\BrandedKey`), the light/dark theme and accent colour (`Themes\Personalize`, `DWM`,
   `Explorer\Accent`), and whether the user disabled the app's startup entry in Task Manager
   (`Explorer\StartupApproved\StartupFolder`).

**Files written.** Two, both in the package's own data folder: the chosen action as JSON, and a
small diagnostic log capped at 256 KB. One more is written **outside** it, and only when the user
turns on the optional notification-area icon: a shortcut named "AntiPilot tray icon.lnk" in the
user's own Startup folder, created with `WScript.Shell`. Turning the option off deletes it. That is
the documented way for a packaged app to have a component start at sign-in when
`Windows.ApplicationModel.StartupTask` cannot be used, and it is what makes the entry appear in Task
Manager → Startup apps, where the user can disable it independently.

**What it does not do.** No network connections and no networking code of any kind. No telemetry, no
analytics, no accounts. It does not remap any key other than the one Windows hands it, and it does
not remove or disable Copilot.

**To test it:** install, then set Settings > Bluetooth & devices > Keyboard > Shortcuts and hotkeys >
"Customize Copilot key on keyboard" to Custom and pick AntiPilot. Open AntiPilot Settings from the
Start menu, choose an action, then press the Copilot key — or Win+C on a keyboard without one.

## Additional license terms

MIT License. See the LICENSE file shipped with the app and shown in its About window.

## Store logo  *(300 x 300, required)*

`packaging\store\StoreListing_300x300.png`

## Screenshot captions  *(max 200 each)*

1. Choose what the Copilot key does — an app, a file or link, a keyboard shortcut, or the Menu key.
2. Pick from every app in your Start menu, Microsoft Store apps included.
3. Give two quick presses their own action, with a wait you control.
4. Change what the key does depending on which app is in front.
5. A quick-launch palette: type to filter, press 1 to 9 to run an entry.
6. Optional notification-area icon, and a switch to bring it back at sign-in.
