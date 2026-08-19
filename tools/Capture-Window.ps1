<#
.SYNOPSIS
    Screenshots AntiPilot's settings window to a PNG.

.DESCRIPTION
    A development aid: the settings window is drawn by hand, so the only way to know whether a
    change looks right is to look at it. Launches the window, waits for it to settle, captures it
    with PrintWindow (which grabs the window whether or not anything is on top of it) and closes it
    again.

.PARAMETER Out
    Where to write the PNG.

.PARAMETER ColorMode
    "dark" or "light" to force one, via ANTIPILOT_COLORMODE. Omit to follow Windows.

.PARAMETER Language
    A BCP-47 tag to capture the window in. Written into the config first.

.EXAMPLE
    .\tools\Capture-Window.ps1 -ColorMode dark
    .\tools\Capture-Window.ps1 -Language ar -Out build\shots\arabic.png
#>
[CmdletBinding()]
param(
    [string]$Out = "$PSScriptRoot\..\build\shots\settings.png",
    [ValidateSet('dark', 'light')]
    [string]$ColorMode,
    [string]$Language,
    [int]$Page = 0,
    [string]$Exe = "$PSScriptRoot\..\src\AntiPilot\bin\Debug\net10.0-windows\AntiPilot.exe",
    [int]$SettleMs = 2500
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

# Only the P/Invokes live in C#. Doing the bitmap work here instead keeps this script off the
# System.Drawing assembly split in .NET 10, which an inline type cannot resolve on its own.
if (-not ('WindowShot' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class WindowShot
{
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    public delegate bool EnumProc(IntPtr hwnd, IntPtr param);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    /// <summary>
    /// The navigation rail, found by shape: it is the only child that is tall and about 184 wide.
    /// Matching on geometry rather than on a caption keeps this out of the app's own code.
    /// </summary>
    public static IntPtr FindRail(IntPtr window)
    {
        IntPtr found = IntPtr.Zero;

        EnumChildWindows(window, (child, _) =>
        {
            RECT r;
            GetWindowRect(child, out r);
            int width = r.Right - r.Left;
            int height = r.Bottom - r.Top;

            if (width > 140 && width < 240 && height > 250)
            {
                found = child;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}
"@
}

if ($Language) {
    $dir = Join-Path $env:LOCALAPPDATA 'AntiPilot'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $configPath = Join-Path $dir 'config.json'
    $config = if (Test-Path $configPath) { Get-Content $configPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{ Schema = 2 } }
    $config | Add-Member -NotePropertyName Language -NotePropertyValue $Language -Force
    $config | ConvertTo-Json -Depth 8 | Set-Content -Path $configPath -Encoding UTF8
}

if ($ColorMode) { $env:ANTIPILOT_COLORMODE = $ColorMode } else { Remove-Item Env:\ANTIPILOT_COLORMODE -ErrorAction SilentlyContinue }

$outDir = Split-Path -Parent $Out
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outPath = Join-Path (Resolve-Path -LiteralPath $outDir).Path (Split-Path -Leaf $Out)

# A settings launch focuses an existing window instead of opening a second one, so a window left
# over from an earlier capture makes this one exit immediately with nothing to photograph.
Get-Process AntiPilot -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$process = Start-Process $Exe -ArgumentList '--settings' -PassThru
try {
    Start-Sleep -Milliseconds $SettleMs
    $process.Refresh()

    if ($process.MainWindowHandle -eq 0) { throw 'The settings window never appeared.' }

    [WindowShot]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500

    if ($Page -gt 0) {
        # Down-arrow the navigation rail rather than clicking it, so the real mouse pointer is
        # left where the user had it.
        $rail = [WindowShot]::FindRail($process.MainWindowHandle)
        if ($rail -eq [IntPtr]::Zero) { throw 'Could not find the navigation rail.' }
        for ($i = 0; $i -lt $Page; $i++) {
            [WindowShot]::SendMessageW($rail, 0x0100, [IntPtr]0x28, [IntPtr]0) | Out-Null
            Start-Sleep -Milliseconds 120
        }
        Start-Sleep -Milliseconds 400
    }

    $rect = New-Object WindowShot+RECT
    [WindowShot]::GetWindowRect($process.MainWindowHandle, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    try {
        # 2 = PW_RENDERFULLCONTENT; without it a composited window comes back blank.
        [WindowShot]::PrintWindow($process.MainWindowHandle, $hdc, 2) | Out-Null
    }
    finally {
        $graphics.ReleaseHdc($hdc)
        $graphics.Dispose()
    }

    $bitmap.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    Write-Host "Captured $outPath ($width x $height)"
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
}
