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
> The manifest sets `BackgroundColor="#0C2F76"` as this file recommends, and references base names
> (`Images\Square44x44Logo.png`); `makepri` resolves the `.scale-*` / `.targetsize-*` variants.


Master art: `Antipilot_master.png`, 1254x1254 with a transparent surround (edit this to rebrand;
everything else is generated from it, with the one exception noted below).

The master used to be `Antipilot_master.svg` and that file is gone. The artwork it drew is not the
artwork that ships: the icon was replaced with a raster one and the vector was never redrawn to
match, so it survived as a picture of the old logo sitting in the folder the docs called the source
of truth. A raster master costs a re-render for any size above 1254 and re-encodes the tiles at
roughly ten times the bytes of the old flat art — about 3.4 MB more in the package, 3% of the
payload. Restoring a vector master is worth doing when there is one that matches.
Palette: navy gradient plate (#114BA6 → #0C2F76), a cyan branch not taken (#26A6FD), and a green
redirect arrow leaving it (#48F871 → #01EEDB) over a dark keycap.

**`targetsize-16` and `targetsize-24` are not generated from the master.** Reduced that far the fork
collapses, the keycap disappears, and what is left is a coloured smear — and those two are the title
bar and the small taskbar views, so they are drawn for the size instead: no keycap, no glow, no
bevel, heavier strokes in proportion, and an arrowhead swept back to a notch, because a plain
triangle on a shaft that thin just reads as the shaft getting wider. Regenerating the pack from the
master will overwrite them, so redraw those two afterwards.

`targetsize-32` deliberately keeps the full artwork, which still has room for the depth. It is the
one that matters most day to day: `AppIcon.FindLogo` resolves every `Load(32)` call to it, so it is
the tray icon and the window icon.

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
    BackgroundColor="#0C2F76">
  <uap:DefaultTile
      Wide310x150Logo="Assets\Wide310x150Logo.png"
      Square71x71Logo="Assets\Square71x71Logo.png"
      Square310x310Logo="Assets\Square310x310Logo.png" />
  <uap:SplashScreen Image="Assets\SplashScreen.png" BackgroundColor="#0C2F76" />
</uap:VisualElements>
```

Visual Studio / MSIX Packaging Tool resolves the `.scale-*` and `.targetsize-*` suffixes automatically,
so reference the base name (e.g. `Square150x150Logo.png`) in the manifest — don't rename the files.
