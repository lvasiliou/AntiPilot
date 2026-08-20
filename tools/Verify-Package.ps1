<#
.SYNOPSIS
    Reports what is actually inside an AntiPilot package: version, languages, and which features
    the binary contains.

.DESCRIPTION
    "Is this the build I think it is?" is a question that comes up every release and is miserable
    to answer by clicking around the app. This reads it out of the bytes instead.

    Point it at an installed package (the default), or at a .msix / .msixbundle file — including
    one downloaded from Partner Center — to compare what shipped against what you built.

.PARAMETER Path
    A .msix or .msixbundle to inspect. Omit to inspect the installed AntiPilot instead.

.EXAMPLE
    .\tools\Verify-Package.ps1
    .\tools\Verify-Package.ps1 -Path build\out\AntiPilot.Store.msixbundle
#>
[CmdletBinding()]
param(
    [string]$Path
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Types that only exist in a given release. Absence is the useful signal: it says the binary
# predates that work, whatever the version number claims.
$markers = [ordered]@{
    'HotkeyDefinition'  = 'keyboard shortcut action'
    'TapCoordinator'    = 'double press'
    'WindowFinder'      = 'launch-or-focus and per-app rules'
    'NavigationRail'    = 'Fluent settings window'
    'AccentFromPalette' = 'accent colour fix (RGBA)'
    'OutcomeFor'        = 'issue #1 fix: Nothing does nothing'
}

function Show-Payload([string]$msix, [string]$label) {
    $zip = [IO.Compression.ZipFile]::OpenRead($msix)
    try {
        $manifest = $zip.Entries | Where-Object { $_.FullName -eq 'AppxManifest.xml' }
        $reader = New-Object IO.StreamReader($manifest.Open())
        $xml = [xml]$reader.ReadToEnd()
        $reader.Dispose()

        $id = $xml.Package.Identity
        Write-Host "  $label" -ForegroundColor Cyan
        Write-Host "    Identity  : $($id.Name)"
        Write-Host "    Version   : $($id.Version)"
        Write-Host "    Arch      : $($id.ProcessorArchitecture)"
        Write-Host "    Languages : $(($xml.Package.Resources.Resource.Language) -join ', ')"

        $dll = $zip.Entries | Where-Object { $_.FullName -eq 'AntiPilot.dll' }
        if (-not $dll) { Write-Host "    AntiPilot.dll not found" -ForegroundColor Red; return }

        $stream = $dll.Open()
        $memory = New-Object IO.MemoryStream
        $stream.CopyTo($memory)
        $stream.Dispose()
        $text = [Text.Encoding]::UTF8.GetString($memory.ToArray())
        $memory.Dispose()

        Write-Host "    Contains:"
        foreach ($m in $markers.Keys) {
            $present = $text -like "*$m*"
            $mark = if ($present) { 'yes' } else { 'NO ' }
            $colour = if ($present) { 'Green' } else { 'Red' }
            Write-Host ("      {0}  {1}" -f $mark, $markers[$m]) -ForegroundColor $colour
        }
    }
    finally { $zip.Dispose() }
}

if ($Path) {
    $full = (Resolve-Path -LiteralPath $Path).Path
    Write-Host "File: $full" -ForegroundColor Yellow
    $file = Get-Item $full
    Write-Host ("      {0:N1} MB, built {1}" -f ($file.Length / 1MB), $file.LastWriteTime)

    if ($full -like '*.msixbundle') {
        $temp = Join-Path $env:TEMP ("apverify-" + [Guid]::NewGuid().ToString('N').Substring(0, 6))
        New-Item -ItemType Directory -Force -Path $temp | Out-Null
        try {
            $bundle = [IO.Compression.ZipFile]::OpenRead($full)
            try {
                foreach ($entry in $bundle.Entries | Where-Object { $_.FullName -like '*.msix' }) {
                    $inner = Join-Path $temp $entry.Name
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $inner, $true)
                    Show-Payload $inner $entry.Name
                }
            }
            finally { $bundle.Dispose() }
        }
        finally { [IO.Directory]::Delete($temp, $true) }
    }
    else {
        Show-Payload $full (Split-Path -Leaf $full)
    }

    return
}

$installed = @(Get-AppxPackage -Name '*AntiPilot*')
if ($installed.Count -eq 0) {
    Write-Host "No AntiPilot package is installed. Install it, or pass -Path to inspect a file." -ForegroundColor Yellow
    return
}

foreach ($p in $installed) {
    Write-Host "Installed: $($p.PackageFullName)" -ForegroundColor Yellow
    Write-Host "    Version   : $($p.Version)"
    Write-Host "    Family    : $($p.PackageFamilyName)"
    Write-Host "    Signature : $($p.SignatureKind)   (Store = came from the Store)"
    Write-Host "    Installed : $($p.InstallDate)"

    $dll = Join-Path $p.InstallLocation 'AntiPilot.dll'
    if (-not (Test-Path $dll)) { Write-Host "    AntiPilot.dll not found" -ForegroundColor Red; continue }

    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($dll))
    Write-Host "    Contains:"
    foreach ($m in $markers.Keys) {
        $present = $text -like "*$m*"
        $mark = if ($present) { 'yes' } else { 'NO ' }
        $colour = if ($present) { 'Green' } else { 'Red' }
        Write-Host ("      {0}  {1}" -f $mark, $markers[$m]) -ForegroundColor $colour
    }
}
