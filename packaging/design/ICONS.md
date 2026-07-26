# Antipilot — Microsoft Store icon pack

> **How this pack maps onto the repo** (added when the pack was imported):
>
> | Supplied file                | Lives in            | Why                                                    |
> | ---------------------------- | ------------------- | ------------------------------------------------------ |
> | `Square*`, `Wide*`, `StoreLogo*` | `packaging\Images\` | shipped inside the MSIX; referenced by AppxManifest.xml |
> | `StoreListing_300x300.png`   | `packaging\store\`  | uploaded to Partner Center, not part of the package     |
> | `Antipilot_master.*`, preview | `packaging\design\` | sources, never shipped                                 |
> | `SplashScreen.*`             | not imported        | a `Windows.FullTrustApplication` entry point never shows a splash screen, so it would be dead weight in every package |
>
> The manifest sets `BackgroundColor="#0F172A"` as this file recommends, and references base names
> (`Images\Square44x44Logo.png`); `makepri` resolves the `.scale-*` / `.targetsize-*` variants.


Master art: `Assets/Antipilot_master.svg` (edit this to rebrand; everything else is generated from it).
Palette: graphite gradient background (#273244 → #0B1220), white keycap, emerald redirect arrow (#059669 / #10B981).

## What's included (48 files, all PNG unless noted)
- **Square44x44Logo** — app-list / taskbar icon. scale-100/125/150/200/400 (44→176px) plus
  targetsize-16/24/32/48/256, each with an `_altform-unplated` twin (transparent, for the taskbar).
- **Square71x71Logo / Square150x150Logo / Square310x310Logo** — small / medium / large Start tiles, all five scales, icon centred with padding so the tile background colour shows through.
- **Wide310x150Logo** — wide tile, all five scales.
- **SplashScreen** — 620×300 at all five scales.
- **StoreLogo** — 50×50 at all five scales (used in the Store + installer).
- **StoreListing_300x300.png** — Partner Center listing icon (300×300).
- **Antipilot_master_1024.png** — high-res export for anything else.

## Manifest snippet (Package.appxmanifest)
Set the tile background to match the icon so the padded tiles blend in:

```xml
<uap:VisualElements
    DisplayName="Antipilot"
    Square150x150Logo="Assets\Square150x150Logo.png"
    Square44x44Logo="Assets\Square44x44Logo.png"
    Description="Reclaim the Copilot key — launch what you choose."
    BackgroundColor="#0F172A">
  <uap:DefaultTile
      Wide310x150Logo="Assets\Wide310x150Logo.png"
      Square71x71Logo="Assets\Square71x71Logo.png"
      Square310x310Logo="Assets\Square310x310Logo.png" />
  <uap:SplashScreen Image="Assets\SplashScreen.png" BackgroundColor="#0F172A" />
</uap:VisualElements>
```

Visual Studio / MSIX Packaging Tool resolves the `.scale-*` and `.targetsize-*` suffixes automatically,
so reference the base name (e.g. `Square150x150Logo.png`) in the manifest — don't rename the files.
