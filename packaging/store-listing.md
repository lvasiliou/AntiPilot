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

Windows 11 offers to point the Copilot key at "any app", then shows you a picker with one entry.
AntiPilot is built to appear in that picker: it registers as a Copilot hardware key provider, then
hands the key press straight on to whatever you actually want — an installed app, a program, a
folder, a link — or turns the key back into the Menu key that laptop keyboards dropped.

## Description  *(max 10,000)*

**Your Copilot key. Your app.**

Windows 11 will let you customise the Copilot key, but only to an app that is MSIX packaged, signed,
and registered as a Copilot hardware key provider. Almost no app bothers, which is why the picker
under Settings → Bluetooth & devices → Keyboard is usually empty apart from Copilot itself.

AntiPilot does bother. Choose it once in Settings, then decide what the key does:

**Launch an installed app**
Anything in the Start menu's app list, Microsoft Store apps included. Pick it from a searchable list
with real icons — no hunting for install paths.

**Launch a program, file, folder or link**
Any path or URL, with optional arguments and a start-in folder. Environment variables like
%USERPROFILE% are expanded.

**Act as the Menu key**
The context-menu key that vanished from modern keyboards. AntiPilot sends the real thing, so the
context menu opens for whatever has focus — exactly like a right-click.

**Out of your way**

A key press starts AntiPilot, does the one thing, and exits. Nothing sits in memory waiting for a
keystroke, and no window appears unless you have not set anything up yet. If you would rather have
it in reach, one checkbox puts an icon in the notification area and brings it back at sign-in — off
by default.

The settings window follows your Windows light or dark theme, tells you at a glance whether the key
is currently pointed at AntiPilot, and takes you straight to the Windows page where that is set.

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
3. Or turn the key back into the Menu key, for the context menu of whatever has focus
4. Answers Win+C as well, for keyboards without a Copilot key
5. Nothing runs in the background — the key press starts it, it acts, it exits
6. Optional notification-area icon, off by default, with a sign-in switch
7. Follows the Windows light and dark theme
8. Searchable app picker with real Windows icons
9. Shows whether the Copilot key currently points at AntiPilot, and links straight to that setting
10. Open source, MIT licensed

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

- One entry in the Start menu; the key target no longer clutters the app list.
- A single switch for the notification-area icon: it appears the moment you tick it and goes away
  the moment you untick it.
- Dark theme support throughout, and a new icon set.
- About window with version, package identity and the MIT licence.

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

AntiPilot is a packaged desktop (Win32) app, so it declares `runFullTrust`. That capability is used
for exactly two things:

1. Starting the app, file or link the user picked in AntiPilot's settings, via
   IApplicationActivationManager or ShellExecute.
2. Sending the Menu key (VK_APPS) with SendInput, when the user chooses that action, so the context
   menu opens for the window they are using.

It declares no other capabilities. The app makes no network connections and contains no networking
code; it collects, transmits and shares nothing. It stores two files in its own package data folder:
the action the user chose, and a small local diagnostic log.

To test it: install, then set Settings > Bluetooth & devices > Keyboard > Shortcuts and hotkeys >
"Customize Copilot key on keyboard" to Custom and pick AntiPilot. Open AntiPilot Settings from the
Start menu, choose an action, then press the Copilot key — or Win+C on a keyboard without one.

## Additional license terms

MIT License. See the LICENSE file shipped with the app and shown in its About window.

## Store logo  *(300 x 300, required)*

`packaging\store\StoreListing_300x300.png`

## Screenshot captions  *(max 200 each)*

1. Choose what the Copilot key does — an installed app, a file or link, or the Menu key.
2. Pick from every app in your Start menu, Microsoft Store apps included.
3. Optional notification-area icon, and a switch to bring it back at sign-in.
