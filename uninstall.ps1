<#
.SYNOPSIS
    Removes the AntiPilot package (and optionally its settings and signing certificate).
#>
[CmdletBinding()]
param(
    [switch]$RemoveSettings,
    [switch]$RemoveCertificate,
    [string]$CertSubject = "CN=AntiPilot Development"
)

$ErrorActionPreference = 'Stop'

# The tray icon keeps a process alive; the sign-in shortcut would point at an AUMID that no
# longer exists. Both have to go before the package does.
Get-Process -Name AntiPilot -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping AntiPilot (pid $($_.Id)) ..." -ForegroundColor DarkGray
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}

$shortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'AntiPilot tray icon.lnk'
if (Test-Path $shortcut) {
    Remove-Item $shortcut -Force
    Write-Host "Removed the sign-in shortcut." -ForegroundColor DarkGray
}

# Wildcard so this also finds the Store-identity package (5676LambrosVasiliou.AntiPilot).
$packages = @(Get-AppxPackage -Name '*AntiPilot*')
if ($packages.Count -eq 0) {
    Write-Host "AntiPilot is not installed." -ForegroundColor DarkGray
}

foreach ($package in $packages) {
    Write-Host "Removing $($package.PackageFullName) ..." -ForegroundColor Cyan
    Remove-AppxPackage -Package $package.PackageFullName
}

if ($RemoveSettings) {
    $dirs = @(Join-Path $env:LOCALAPPDATA 'AntiPilot')
    $dirs += Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter '*AntiPilot*' `
        -ErrorAction SilentlyContinue | ForEach-Object FullName

    foreach ($dir in $dirs) {
        if (Test-Path $dir) {
            Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Removed $dir" -ForegroundColor DarkGray
        }
    }

    # Windows remembers a notification-area entry per executable path, so an entry piles up for
    # every version that ever ran.
    $notify = 'HKCU:\Control Panel\NotifyIconSettings'
    if (Test-Path $notify) {
        Get-ChildItem $notify | Where-Object {
            (Get-ItemProperty $_.PSPath).ExecutablePath -match 'AntiPilot'
        } | ForEach-Object {
            Remove-Item $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Removed tray icon entry $($_.PSChildName)" -ForegroundColor DarkGray
        }
    }
}

if ($RemoveCertificate) {
    Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
        Where-Object Subject -eq $CertSubject |
        ForEach-Object {
            Write-Host "Removing certificate $($_.Thumbprint) from $($_.PSParentPath)" -ForegroundColor DarkGray
            Remove-Item $_.PSPath -Force
        }
}
