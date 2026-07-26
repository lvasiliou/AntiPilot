# AntiPilot

Makes the Windows 11 **Copilot key** (and <kbd>Win</kbd>+<kbd>C</kbd>) launch whatever you want —
a desktop program, a Microsoft Store app, or the old **Menu / context-menu key**.

## Why a whole app is needed

Settings → Bluetooth & devices → Keyboard → Shortcuts and hotkeys → *Customize Copilot key on
keyboard* offers "Custom", but the app picker is nearly always empty apart from Copilot itself. That is not a bug. Windows only lists an
app if **both** of these are true:

1. the app is **MSIX packaged and signed**, and
2. its package manifest declares the `com.microsoft.windows.copilotkeyprovider` app extension.

Practically no shipping app declares that extension, hence a picker with one entry. AntiPilot
declares it and then acts as a trampoline to whatever you actually want.

### How Windows actually launches it

The documentation says providers are started through **URI activation** (`antipilot-key://?state=…`),
and the manifest registers that scheme. In practice, on Windows 11 26200 the key simply **launches
the provider's AUMID with no command line at all** — measured, not guessed. AntiPilot handles both.

Because a bare launch carries no arguments, one executable serves three manifest `<Application>`
entries and works out its job from `GetCurrentApplicationUserModelId()`:

| Entry       | Started by                        | Behaviour                             |
| ----------- | --------------------------------- | ------------------------------------- |
| `AntiPilot` | the Copilot key / Win+C           | runs the configured action, no window |
| `Settings`  | **AntiPilot Settings** in Start   | opens the settings window             |
| `Tray`      | the sign-in shortcut, or settings | notification-area icon                |

So the Start menu shows two entries. That is not tidiness losing an argument — it is forced:

- **The key target has to stay in the app list.** Giving it `AppListEntry="none"` also drops it from
  the "Customize Copilot key" picker. Nothing warns you: the extension stays registered
  (`AppExtensionCatalog.Open("com.microsoft.windows.copilotkeyprovider")` still returns it) and an
  already-stored choice keeps launching the app, so the key goes on working while quietly becoming
  impossible to re-select. Only `Tray` is hidden.
- **It cannot double as the settings entry.** Windows launches the key target by AUMID with no
  arguments, exactly as the Start menu does, so the two are indistinguishable from inside the
  process. Whatever that entry does on a key press, it also does when clicked in Start — and running
  the action is the behaviour that matters.

The URI form still works where Windows uses it, and every state runs the same action:

| Manifest element    | URI                          | What AntiPilot does                     |
| ------------------- | ---------------------------- | --------------------------------------- |
| `SingleTap`         | `antipilot-key://?state=Tap` | runs the action                          |
| `PressAndHoldStart` | `…?state=Down`               | runs the action                          |
| `PressAndHoldStop`  | `…?state=Up`                 | ignored, so a long press acts only once  |

There is no separate press-and-hold action. Telling a long press from a short one needs the URI
states above, and the machines that matter never send them — a hold action would silently do nothing.

## What it can do

- **Launch an installed app** — anything in the Start menu's app list, Store apps included. Packaged
  apps go through `IApplicationActivationManager::ActivateApplication`; classic entries are handed
  to the shell as `shell:AppsFolder\…`.
- **Launch a program, file, folder or link** — any path or URL, with optional arguments and working
  directory. Environment variables are expanded.
- **Act as the Menu key** — synthesises `VK_APPS`, so the context menu of whatever is focused opens,
  exactly like a right-click.

It follows the **Windows light/dark theme** and never opens a window on a key press unless nothing is
configured yet. Nothing runs in the background: each press starts the app, does the thing, and exits.

An optional **notification-area icon** (settings, run the action, status) is **off by default** — a
resident process just to host one icon is a poor trade when the Start menu already opens the
settings window. It has one checkbox for it, *"Show the tray icon, and start it when I sign in"*:
ticking it puts the icon up immediately, unticking takes it away immediately. Since the icon lives in
its own process, "take it away" is a named event (`Local\AntiPilot.TrayExit`) that the tray process
waits on — or, when the settings window is the tray's own child window, a direct exit once that
window closes.

The obvious mechanism, `Windows.ApplicationModel.StartupTask`, turns out to be unusable here: it
resolves a task id against the `<Application>` entry that is *running*, so a task declared on the
hidden `Tray` entry is invisible to the settings window (`Couldn't find a StartupTask in the appx
manifest with the input taskId`), and declaring it on `Settings` instead would pop the settings
window open at every sign-in. AntiPilot writes a **Startup-folder shortcut** instead, pointing at
`explorer.exe shell:AppsFolder\<family>!Tray` — an AUMID rather than a path, because the MSIX install
directory carries the package version and would break on every update. MSIX's AppData redirection
does not catch that folder (verified). It lands in the same **Task Manager → Startup apps** list; if
you disable it there, Windows records that next to the shortcut and the checkbox says so rather than
silently fighting you.

Expect it to be invisible at first: Windows 11 files brand new notification icons into the hidden
overflow (`^` next to the clock) and only promotes them once you drag them onto the taskbar. That
choice is stored per executable path under `HKCU\Control Panel\NotifyIconSettings`, and an MSIX
install path contains the version number — so a version bump makes Windows treat it as a new icon
and hide it again.

## Build

Needs the .NET 10 SDK and the Windows 10/11 SDK (for `makeappx`, `makepri` and `signtool`).

```powershell
.\build.ps1
```

This publishes the app self-contained (logos included, see the `Content` item in the csproj), indexes
resources with `makepri`, packs `build\out\AntiPilot.msix`, and signs it with a self-signed
certificate created in `Cert:\CurrentUser\My` on first run.

### Building for the Microsoft Store

```powershell
.\build.ps1 -Target Store
```

Produces `build\out\AntiPilot.Store.msixbundle` — x64 and arm64, **unsigned**, which is what Partner
Center wants because the Store signs packages itself. ARM64 matters here: a large share of
Copilot-key hardware is Copilot+ PCs on Snapdragon, and an x64-only package would run there under
emulation on every key press. The three identity values are stamped from Partner Center →
Product identity and must match character for character:

| Manifest field                          | Value                                     |
| --------------------------------------- | ----------------------------------------- |
| `Identity/Name`                          | `5676LambrosVasiliou.AntiPilot`            |
| `Identity/Publisher`                     | `CN=E4150ECD-C5C0-4302-91B1-E90B7B7F602B`  |
| `Properties/PublisherDisplayName`        | `Lambros Vasiliou`                         |

They live as defaults in [build.ps1](build.ps1) (`-StoreIdentityName`, `-StorePublisher`,
`-PublisherDisplayName`), so the checked-in manifest keeps the sideload identity and nothing has to
be edited by hand to switch between the two.

To try the exact package before uploading it, `-SignForTesting` signs it with a self-signed
certificate whose subject matches the Store publisher GUID — a package can only be signed by a
certificate whose subject equals its `Identity/Publisher`. Do not upload that one.

### Publishing the privacy policy

Partner Center requires a privacy policy URL for this submission because the package declares the
`runFullTrust` capability — the requirement follows from the capability, not from anything the app
actually collects (it collects nothing). The text is in [PRIVACY.md](PRIVACY.md); it needs to live at
a public URL. Quickest routes, no web host needed:

- **GitHub repository + Pages** — push this repo, then Settings → Pages → deploy from the default
  branch. `PRIVACY.md` is served as a rendered page at
  `https://<user>.github.io/AntiPilot/PRIVACY`.
- **A public gist** — paste the text into <https://gist.github.com> and use its URL. Fastest option,
  no repository required.
- Any static page you already control.

Whichever you pick, the URL must stay reachable while the app is listed.

Worth knowing before submitting:

- **Versions come from the date.** With no `-Version`, the build number is
  `(year - 2020) * 1000 + day of year`, so 26 July 2026 gives `1.1.6207.0`. It only ever increases
  and stays under the 65535 ceiling until 2085. Sideload builds put two-minute blocks since midnight
  in the revision so several builds a day still install over each other; Store builds keep it at `0`,
  which the Store requires. Pass `-Version` to override, or `-VersionPrefix 2.0` to move the
  major.minor along.
- **`runFullTrust`** is a restricted capability. Submission asks you to justify it; the honest
  answer is that a Copilot key provider has to be a packaged desktop app.
- **The sideload build and the Store build are different packages** (different identity, different
  family name). With both installed you get two AntiPilot entries in the Copilot key picker, and
  the key points at whichever one you last selected. Uninstall the sideload build first.
- **Architectures** default to x64 + arm64 for the Store and x64 for sideloading; `-Architectures`
  overrides either. Two or more produce an `.msixbundle`, one produces a plain `.msix`.

## Install

The signing certificate has to be trusted machine-wide, which needs administrator rights:

```powershell
.\install.ps1
```

Then:

1. Open **Settings → Bluetooth & devices → Keyboard → Shortcuts and hotkeys**, set *Customize
   Copilot key on keyboard* to **Custom** and pick **AntiPilot**. The *Open Windows settings* button
   in the settings window deep-links straight to it
   (`ms-settings:personalization-textinput-copilot-hardwarekey` — the URI kept its old name after
   the setting moved out of Personalization).
2. Open **AntiPilot** from the Start menu and choose what the key does.
3. Press the Copilot key — or <kbd>Win</kbd>+<kbd>C</kbd> if your keyboard has no Copilot key.

To remove it again:

```powershell
.\uninstall.ps1 -RemoveSettings -RemoveCertificate
```

## Layout

```
src/AntiPilot/            the app: trampoline + tray icon + settings UI (WinForms, .NET 10)
  Program.cs              entry point; picks key-press / settings / tray from args or AUMID
  ActionRunner.cs         carries out a configured action
  AppConfig.cs            settings model, stored as JSON
  CopilotKeyStatus.cs     reads HKCU\…\Shell\BrandedKey to report the current key target
  Interop/                SendInput, app activation, Apps-folder enumeration and icons
  UI/                     settings window, action editor, app picker, tray icon, theme
packaging/AppxManifest.xml  three entry points, the key-provider extension, one capability
packaging/Images/         logos shipped *inside* the MSIX — scale-* and targetsize-* variants,
                          copied into the build by the Content item in AntiPilot.csproj and
                          resolved from their base names by makepri
packaging/store/          artwork uploaded to Partner Center, never packaged (listing icon,
                          and screenshots when you take them)
packaging/design/         sources: master SVG, 1024px export, preview sheet, ICONS.md
packaging/store-listing.md  Store listing copy, per Partner Center field
packaging/Public/         empty folder the copilotkeyprovider extension requires
build.ps1 install.ps1 uninstall.ps1
```

## Notes and limits

- **Settings location.** Unpackaged: `%LOCALAPPDATA%\AntiPilot\config.json`. Installed as MSIX,
  Windows redirects that to `%LOCALAPPDATA%\Packages\AntiPilot_…\LocalCache\Local\AntiPilot\`.
  `antipilot.log` in the same folder records every key activation — the *Open log* button in the
  settings window opens it.
- **Elevated windows.** Windows blocks synthetic input aimed at processes running as administrator,
  so the Menu key action does nothing while an elevated window is focused. This is a UIPI rule, not
  something an app can opt out of.
- **Theme.** Follows the Windows app theme, read at start-up; reopen the window after switching
  themes. `ANTIPILOT_COLORMODE=dark` or `=light` forces one regardless of the system setting.
- **Self-signed certificate.** Fine for your own machine. Distributing the package to others means
  either signing with a certificate they trust or shipping it through the Store.
- **Version bumps.** `Add-AppxPackage` refuses to reinstall the same version over itself, so pass a
  new one when iterating: `.\build.ps1 -Version 1.0.1.0`.

## What is in this repository, and what is not

Nothing here is a secret, and that is deliberate rather than lucky:

- **The signing key never touches the repo.** `build.ps1` creates and uses a certificate in
  `Cert:\CurrentUser\My`; only the public `.cer` is exported, into the ignored `build\` folder.
  `.gitignore` blocks `*.pfx`, `*.p12`, `*.snk` and `*.cer` regardless.
- **No settings or logs.** They live in the app's package data folder on each machine, never here.
- **The Store identity is public information.** `Identity/Name`, the publisher GUID, the package
  family name and the Store ID appear in `build.ps1`, the README and `store-listing.md`. Every one of
  them is visible to anyone who installs the app — `Get-AppxPackage` prints the family name, and the
  Store ID is in the product URL. They are identifiers, not credentials: publishing under that
  identity needs the Partner Center account, and signing as that publisher needs a certificate issued
  to it. Keeping them in the repo is what lets the build verify the identity before an upload.
- **A name is published**, in the MIT copyright line and `PublisherDisplayName` — unavoidable for a
  Store listing. No email address: the privacy policy points at the repository's issues instead, so
  fill in the repository URL in `PRIVACY.md` before publishing the policy.

## Licence

MIT — see [LICENSE](LICENSE). The same text ships next to the executable and is shown by the
**About** window (footer of the settings window, or the tray menu).

