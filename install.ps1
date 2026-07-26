<#
.SYNOPSIS
    Trusts the AntiPilot signing certificate and installs the package.

.DESCRIPTION
    Adding the certificate to LocalMachine\TrustedPeople needs administrator rights;
    the rest does not. Run this from an elevated PowerShell after build.ps1.
#>
[CmdletBinding()]
param(
    [string]$Msix = (Join-Path $PSScriptRoot 'build\out\AntiPilot.msix'),
    [string]$Certificate = (Join-Path $PSScriptRoot 'build\out\AntiPilot.cer')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Msix)) { throw "Package not found: $Msix. Run build.ps1 first." }

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$isAdmin = ([Security.Principal.WindowsPrincipal]$identity).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

$alreadyTrusted = $false
if (Test-Path $Certificate) {
    $thumb = (Get-PfxCertificate -FilePath $Certificate).Thumbprint
    $alreadyTrusted = [bool](Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
        Where-Object Thumbprint -eq $thumb)
}

if (-not $alreadyTrusted) {
    if (-not $isAdmin) {
        throw "The signing certificate is not trusted yet. Re-run this script as administrator."
    }

    Write-Host "Trusting $Certificate ..." -ForegroundColor Cyan
    Import-Certificate -FilePath $Certificate -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}
else {
    Write-Host "Signing certificate is already trusted." -ForegroundColor DarkGray
}

Write-Host "Installing $Msix ..." -ForegroundColor Cyan
Add-AppxPackage -Path $Msix -ForceUpdateFromAnyVersion

# Wildcard: the Store build carries the reserved identity name, not plain "AntiPilot".
$package = Get-AppxPackage -Name '*AntiPilot*' | Select-Object -First 1
if (-not $package) { throw "Installation reported success but the package is not registered." }

Write-Host ""
Write-Host "Installed." -ForegroundColor Green
Write-Host "  Package family: $($package.PackageFamilyName)"
Write-Host "  AUMID:          $($package.PackageFamilyName)!AntiPilot"
Write-Host ""
Write-Host "Now open Settings > Bluetooth & devices > Keyboard > Shortcuts and hotkeys," -ForegroundColor Cyan
Write-Host "set 'Customize Copilot key on keyboard' to Custom and pick AntiPilot:" -ForegroundColor Cyan
Write-Host "  start ms-settings:personalization-textinput-copilot-hardwarekey" -ForegroundColor DarkGray
Write-Host "Then open 'AntiPilot' from the Start menu to choose what it does." -ForegroundColor Cyan
Write-Host ""
Write-Host "Nothing runs in the background: the key press starts AntiPilot and it exits again." -ForegroundColor DarkGray
Write-Host "For a tray icon, tick 'Show the tray icon, and start it when I sign in' in the" -ForegroundColor DarkGray
Write-Host "settings window. Windows hides new tray icons - look under the ^ next to the" -ForegroundColor DarkGray
Write-Host "clock and drag it onto the taskbar." -ForegroundColor DarkGray
