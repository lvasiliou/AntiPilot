# AntiPilot

Makes the Windows 11 **Copilot key** (and <kbd>Win</kbd>+<kbd>C</kbd>) do whatever you want — launch
a desktop program or a Microsoft Store app, send a keyboard shortcut, open a small launcher, or act
as the old **Menu / context-menu key**. Two quick presses can do something different from one, and
the key can change its mind depending on which app is in front.

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

There is no press-and-hold action. Telling a long press from a short one needs the URI states above,
and the machines that matter never send them — a hold action would silently do nothing.

### Two presses, though, *are* distinguishable

A **double press** is detectable without any help from Windows, and AntiPilot offers it as a second
action. Since every press is a brand new process, the two never meet in memory; they meet through
two named kernel objects instead. The first press takes a mutex and waits on an event; a second
press finds the mutex already held, sets the event and exits without doing anything itself. The
first press then runs the double action rather than the single one.

The catch is stated plainly in the UI rather than buried: **the wait is unconditional.** A single
press has to sit out the whole window to find out that no second press is coming, so turning this on
makes every press that much slower — the same trade every double-click detector in existence makes.
That is why it is off by default and why the window is adjustable (200–1000 ms, 350 ms default).
Leave it off and there is no delay at all; the check is skipped entirely.

## What it can do

- **Launch an installed app** — anything in the Start menu's app list, Store apps included. Packaged
  apps go through `IApplicationActivationManager::ActivateApplication`; classic entries are handed
  to the shell as `shell:AppsFolder\…`.
- **Launch a program, file, folder or link** — any path or URL, with optional arguments and working
  directory. Environment variables are expanded.
- **Send a keyboard shortcut** — any chord, into whatever has focus: <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Esc</kbd>,
  <kbd>Win</kbd>+<kbd>V</kbd>, Print Screen, the media keys, <kbd>F13</kbd>–<kbd>F24</kbd> for macro
  software. Capture it by pressing it, or pick one of the presets. Scan codes come from
  `MapVirtualKey` and the extended-key flag is set per key, which is what keeps the arrows from
  arriving as the numeric keypad.
- **Act as the Menu key** — synthesises `VK_APPS`, so the context menu of whatever is focused opens,
  exactly like a right-click. (A special case of the above, kept as its own mode because it is the
  reason a lot of people install this.)
- **Open a quick-launch palette** — a small keyboard-first list of your own entries. Type to filter,
  <kbd>1</kbd>–<kbd>9</kbd> to run one outright, <kbd>Esc</kbd> to dismiss. The chosen action runs
  *after* the palette closes, so a shortcut entry lands on the window you were actually using.

Two more things shape what a press does:

- **Launch or focus.** App and program actions can start another copy every time (the default),
  bring an existing window to the front instead, or toggle — front if it is not, minimised if it is.
  Windows belonging to Store apps are hosted by `ApplicationFrameHost`, so matching walks through it
  to the process that really owns the window.
- **Per-app rules.** The key can do something different while a particular app is in front — rules
  are matched on the foreground process name, tried in order, and anything unmatched falls through
  to the ordinary single-press action.

It follows the **Windows light/dark theme**, live: change the theme and open windows repaint rather
than waiting to be reopened. The UI is translated into **ten languages besides English**. It never
opens a window on a key press unless nothing is configured yet, and when an action fails it says so
with a notification-area balloon rather than a dialog that steals focus from whatever you were
typing into. Nothing runs in the background: each press starts the app, does the thing, and exits.

## The settings window

Laid out the way Windows 11 lays settings out: a navigation rail down the left, a page of cards on
the right, the commit buttons along the bottom.

None of that is WinForms' own. WinForms has no card, no toggle switch, no navigation rail, and its
buttons and sliders are drawn by the common controls library, which has looked its age for a decade.
So `UI/Fluent/` is a small design system — rounded surfaces, the WinUI type ramp in Segoe UI
Variable, settings cards, a toggle, a slider, a button with an accent variant, and the rail — all
custom-painted from one set of tokens in [Theme.cs](src/AntiPilot/UI/Theme.cs). The colours are the
ones WinUI specifies rather than ones invented to look close, and the **accent colour is the user's
own**, read from `AccentPalette` in the shade WinUI would pick for the current theme: the light
variant on dark backgrounds, the dark variant on light ones, so text on top of it stays readable
whatever colour they chose.

The alternative was WinUI 3, which would have been the authentic stack and the wrong choice here: it
pulls in the Windows App SDK, adds tens of megabytes to a package that ships per-architecture, and
puts a runtime initialisation in front of a window that a key press might open. This app's whole
premise is that it is small and starts fast.

Because every pixel is hand-drawn, looking at it is the only way to review it —
[tools/Capture-Window.ps1](tools/Capture-Window.ps1) opens the window, screenshots it and closes it
again, in either theme, any language, on any page:

```powershell
.\tools\Capture-Window.ps1 -ColorMode dark -Page 1
```

That is not a nicety. It is how the Arabic layout bug below was found.

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

## Languages

English plus ten others, chosen from the Store acquisition report rather than from a list of big
languages: Russian, Spanish, Simplified Chinese, Brazilian Portuguese, Turkish, Japanese, Korean,
Arabic, Indonesian and Traditional Chinese. English covers 65% of installs on its own; those ten
take it to about 97%.

The language picker is on the **General** page of the settings window. Its default, and the value a
fresh install has, is **"Same as Windows"** — stored as no value at all, so the app follows the
system UI language and keeps following it if that changes. Picking a language pins it.

**Arabic mirrors the window**, and getting that right needed a second attempt. The obvious lever,
WinForms' `RightToLeftLayout`, sets `WS_EX_LAYOUTRTL`, which mirrors the whole device context — and
every Fluent control paints into that context, so the first Arabic build came back with every card
title and button label reversed letter by letter. The fix is to set `RightToLeft` alone, which flips
the standard controls and scrollbars without touching what we draw, and to have the custom controls
mirror their own geometry: icons and the accent pill move to the right edge, the toggle's "on"
position flips, and text is drawn with the reading-order flag. Only a screenshot showed the problem;
no test would have.

All 157 strings live in `tools\strings\en.txt` as plain `Key = text` lines, with one file per
translation. `tools\Update-Strings.ps1` turns them into the `.resx` files the app embeds *and* into
`Strings.g.cs`, which has one property per key — so a mistyped string name is a compile error rather
than a blank label found by a user. Adding a language is one new `<tag>.txt`, then the same tag in
three places:

| Where | Why |
| ----- | --- |
| `tools\strings\<tag>.txt`                       | the translation itself |
| `SatelliteResourceLanguages` in the csproj      | anything unlisted is stripped from the build |
| `<Resources>` in `AppxManifest.xml` and the makepri qualifier list in `build.ps1` | or the package reports itself as English-only |

Miss one and the language disappears quietly, which is why a test asserts that every shipped
language actually resolves to something other than the English text.

Note that .NET ships its own translated WinForms strings for seven of the ten; Arabic and Indonesian
are not among them, so a handful of framework-supplied strings stay English there.

## Build

Needs the .NET 10 SDK and the Windows 10/11 SDK (for `makeappx`, `makepri` and `signtool`).

```powershell
.\build.ps1
```

This publishes the app self-contained (logos included, see the `Content` item in the csproj), indexes
resources with `makepri`, packs `build\out\AntiPilot.msix`, and signs it with a self-signed
certificate created in `Cert:\CurrentUser\My` on first run.

The publish is **ReadyToRun**, which matters here for a reason it would not in a normal app: Windows
starts a fresh process for every key press, so cold-start cost is paid per press rather than once
per session. Measured on x64, twelve runs each, it is 118 ms median without and 107 ms with, for
0.2 MB of package. Most of the gain one might expect is already there — a self-contained publish
ships a precompiled framework, so only AntiPilot's own code was still being jitted — but at that
price the trade is worth making on the one path the user waits for.

### Tests

```powershell
dotnet test
```

95 tests, no UI automation: the parts worth testing are the parts that decide what a key press does.
Chord parsing and formatting round-trip (including which keys need the extended-key prefix, where a
mistake sends <kbd>Num4</kbd> instead of <kbd>←</kbd>); config load, save and the clamping that keeps
a hand-edited double-press window from making the key look broken; rule matching and fall-through;
target validation; that every string resolves in every shipped language. The double-press
coordinator is tested too — it talks through named kernel objects, and threads see those exactly the
way separate processes do, so a second thread stands in for the second press.

CI runs the same on `windows-latest`, plus a check that the generated `.resx` and `Strings.g.cs`
match `tools\strings`, and builds the Store bundle on every run so a broken manifest is caught
before an upload rather than after one.

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
  ActionValidator.cs      checks an action still points at something real, before it is saved
  AppConfig.cs            settings model, stored as JSON
  HotkeyDefinition.cs     parses and formats chords ("Ctrl+Shift+Escape"); no WinForms dependency
  TapCoordinator.cs       tells one press from two, across processes
  CopilotKeyStatus.cs     reads HKCU\…\Shell\BrandedKey to report the current key target
  Strings.g.cs            generated: one property per user-visible string
  Resources/              generated: Strings.resx and one satellite per language
  Interop/                SendInput, app activation, Apps-folder enumeration and icons,
                          window/foreground lookup for focus-or-launch and per-app rules
  UI/                     settings window, action editor, hotkey capture, app picker,
                          rule and palette editors, the palette itself, tray icon
  UI/Theme.cs             the design tokens: Fluent palette, the user's accent, metrics
  UI/Fluent/              the controls WinForms does not have — settings card, toggle,
                          slider, accent button, navigation rail, type ramp, paint helpers
tests/AntiPilot.Tests/    xunit; the decision-making parts, no UI automation
tools/strings/            en.txt and one file per translation — the source of truth
tools/Update-Strings.ps1  generates Resources\*.resx and Strings.g.cs from the above
tools/Capture-Window.ps1  screenshots the settings window, for reviewing the hand-drawn UI
.github/workflows/ci.yml  build, test, string-table check, Store package
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
  settings window opens it. It rotates to `antipilot.log.1` at 256 KB rather than being deleted,
  because the part that explains the problem is usually the part that just scrolled off. Writes are
  serialised with a named mutex: several AntiPilot processes are alive at once by design, and two of
  them for every double press, and appends from different processes were observed interleaving
  *inside* a line.
- **Settings travel.** *Export* and *Import* in the settings window write and read the same JSON, so
  a setup can be moved between machines. The tray introduction is not imported — it is about the
  machine, not the settings.
- **Elevated windows.** Windows blocks synthetic input aimed at processes running as administrator,
  so the Menu key and keyboard-shortcut actions do nothing while an elevated window is focused, and
  "bring the existing window to the front" cannot reach one either. This is a UIPI rule, not
  something an app can opt out of. The escape hatch, `uiAccess`, needs an install location MSIX
  cannot provide, so this is not fixable rather than merely unfixed.
- **The Windows key in a captured shortcut** is a checkbox, not something you press. Windows opens
  the Start menu before any application sees that key, and swallowing it needs a resident low-level
  keyboard hook — a lot of machinery for an app whose whole point is not staying resident.
  <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Del</kbd> is absent entirely: it is the secure attention
  sequence and no synthesised input can trigger it, so offering it would be a button that does
  nothing.
- **Theme.** Follows the Windows app theme and keeps up with changes while a window is open, title
  bar included. It is not quite a fresh start-up — a few standard controls pick their colours when
  their handle is created and only fully catch up next time the window opens.
  `ANTIPILOT_COLORMODE=dark` or `=light` forces one regardless of the system setting.
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

