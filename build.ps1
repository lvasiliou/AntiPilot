<#
.SYNOPSIS
    Builds, packages and (for sideloading) signs AntiPilot as an MSIX.

.DESCRIPTION
    Windows only lists MSIX packaged *and signed* apps in the "Customize Copilot key"
    picker, so a plain .exe is not enough. This script publishes the app, packs it with
    makeappx, and signs it with a self-signed certificate kept in the current user's
    certificate store. Run install.ps1 afterwards (elevated) to trust the certificate
    and install the package.

.PARAMETER Version
    Package version, four parts. Leave it out and one is derived from the date:

        <prefix>.<(year - 2020) * 1000 + day of year>.<revision>

    so 26 July 2026 gives 1.1.6207.0 — always increasing, and it fits the 65535 ceiling on each
    part until 2085. For sideload builds the revision counts two-minute blocks since midnight, so
    rebuilding the same day still installs over itself; Store builds keep the revision at 0,
    because the Store reserves that part and requires it to be zero.

.PARAMETER VersionPrefix
    The major.minor to put in front of the date-derived build number.

.PARAMETER Target
    Sideload (default) builds a self-signed package for this machine. Store stamps the identity
    reserved in Partner Center and leaves the package unsigned, which is what you upload — the
    Store signs it itself.

.PARAMETER Architectures
    Which architectures to publish. Defaults to x64 for sideloading and x64 + arm64 for the Store,
    since a good share of Copilot-key hardware is ARM64. More than one produces an .msixbundle.

.PARAMETER SignForTesting
    Only meaningful with -Target Store: signs the Store-identity package with a self-signed
    certificate whose subject matches the Store publisher, so the exact package you are about to
    upload can be installed here first. Never upload the result; upload the unsigned build.

.PARAMETER SkipSign
    Produce an unsigned package (inspection only; Windows will not install it).

.EXAMPLE
    .\build.ps1                                      # sideload build, version from today's date
    .\build.ps1 -Target Store                        # unsigned x64 + arm64 bundle for Partner Center
    .\build.ps1 -Target Store -Architectures x64     # unsigned, x64 only
    .\build.ps1 -Version 2.0.0.0                     # explicit version wins
#>
[CmdletBinding()]
param(
    [string]$Version,

    [string]$VersionPrefix = "1.1",

    [ValidateSet('Sideload', 'Store')]
    [string]$Target = 'Sideload',

    [ValidateSet('x64', 'arm64')]
    [string[]]$Architectures,

    [string]$CertSubject = "CN=AntiPilot Development",

    # From Partner Center > Product identity. These must match character for character or the
    # submission is rejected. StoreFamilyName is not used to build anything: it is checked against
    # the family name these values actually hash to, so a mistyped GUID fails here instead of
    # after a 90 MB upload.
    [string]$StoreIdentityName = "5676LambrosVasiliou.AntiPilot",
    [string]$StorePublisher = "CN=E4150ECD-C5C0-4302-91B1-E90B7B7F602B",
    [string]$StoreFamilyName = "5676LambrosVasiliou.AntiPilot_ry1r8aenh16n2",
    [string]$PublisherDisplayName = "Lambros Vasiliou",

    [switch]$SignForTesting,
    [switch]$SkipSign
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$buildDir = Join-Path $root 'build'
$stageRoot = Join-Path $buildDir 'stage'
$outDir = Join-Path $buildDir 'out'
$packagesDir = Join-Path $outDir 'packages'
$cerPath = Join-Path $outDir 'AntiPilot.cer'

$store = $Target -eq 'Store'
$identityName = if ($store) { $StoreIdentityName } else { 'AntiPilot' }
$identityPublisher = if ($store) { $StorePublisher } else { $CertSubject }

if (-not $Architectures) {
    $Architectures = if ($store) { @('x64', 'arm64') } else { @('x64') }
}

# The Store signs what you upload; signing it here would only get in the way.
if ($store -and -not $SignForTesting) { $SkipSign = $true }

# The package family name is <Identity/Name>_<hash of Identity/Publisher>: SHA-256 of the publisher
# as UTF-16, first 64 bits, written in Crockford-style base32. Computing it here catches a wrong
# publisher string before Partner Center does.
function Get-PublisherId([string]$publisher) {
    $hash = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::Unicode.GetBytes($publisher))
    $bits = (-join ($hash[0..7] | ForEach-Object { [Convert]::ToString($_, 2).PadLeft(8, '0') })) + '0'
    $alphabet = '0123456789abcdefghjkmnpqrstvwxyz'
    $id = ''
    for ($i = 0; $i -lt 65; $i += 5) { $id += $alphabet[[Convert]::ToInt32($bits.Substring($i, 5), 2)] }
    return $id
}

if ($store -and $StoreFamilyName) {
    $computed = "$identityName`_$(Get-PublisherId $identityPublisher)"
    if ($computed -ne $StoreFamilyName) {
        throw @"
Identity does not match Partner Center.
  Identity/Name      : $identityName
  Identity/Publisher : $identityPublisher
  gives family name  : $computed
  Partner Center says: $StoreFamilyName
Check both strings against Partner Center > Product identity.
"@
    }

    Write-Host "  Family name:   $computed (matches Partner Center)" -ForegroundColor DarkGray
}

if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Version must have four parts, e.g. 1.1.6207.0." }
    if ($store -and -not $Version.EndsWith('.0')) { throw "The Store requires the fourth part to be 0." }
}
else {
    $now = Get-Date
    $dateBuild = (($now.Year - 2020) * 1000) + $now.DayOfYear
    # Store: the revision must be 0. Sideload: two-minute blocks since midnight, so several
    # builds in one day still count upwards and install over each other.
    $revision = if ($store) { 0 } else { [int]($now.TimeOfDay.TotalMinutes / 2) }
    $Version = "$VersionPrefix.$dateBuild.$revision"
}

$bundle = $Architectures.Count -gt 1
$artifactName = if ($store) { 'AntiPilot.Store' } else { 'AntiPilot' }
$artifactPath = Join-Path $outDir ("$artifactName." + $(if ($bundle) { 'msixbundle' } else { 'msix' }))

Write-Host "Target: $Target" -ForegroundColor Cyan
Write-Host "  Identity:      $identityName" -ForegroundColor DarkGray
Write-Host "  Publisher:     $identityPublisher" -ForegroundColor DarkGray
Write-Host "  Version:       $Version" -ForegroundColor DarkGray
Write-Host "  Architectures: $($Architectures -join ', ')" -ForegroundColor DarkGray

function Find-SdkTool([string]$name) {
    $binRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
    if (-not (Test-Path $binRoot)) { throw "Windows SDK not found. Install the Windows 10/11 SDK." }

    $candidate = Get-ChildItem $binRoot -Directory |
        Where-Object { $_.Name -match '^10\.' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$name" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if (-not $candidate) { throw "Could not find $name in the Windows SDK." }
    return $candidate
}

function Invoke-Tool([string]$exe, [string[]]$toolArgs) {
    Write-Host "  $([IO.Path]::GetFileName($exe)) $($toolArgs -join ' ')" -ForegroundColor DarkGray
    & $exe @toolArgs
    if ($LASTEXITCODE -ne 0) { throw "$exe failed with exit code $LASTEXITCODE" }
}

$makepri = Find-SdkTool 'makepri.exe'
$makeappx = Find-SdkTool 'makeappx.exe'

foreach ($dir in @($packagesDir)) {
    if (Test-Path $dir) { Get-ChildItem $dir -File | Remove-Item -Force }
    else { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

$assemblyVersion = ($Version.Split('.')[0..2] -join '.')
$priConfig = Join-Path $buildDir 'priconfig.xml'
Invoke-Tool $makepri @('createconfig', '/cf', $priConfig, '/dq', 'en-US', '/o')

foreach ($arch in $Architectures) {

    Write-Host ""
    Write-Host "=== $arch ===" -ForegroundColor Cyan

    # --- publish ------------------------------------------------------------

    Write-Host "Publishing..." -ForegroundColor Cyan
    $publishDir = Join-Path $buildDir "publish\$arch"
    if (Test-Path $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }

    dotnet publish (Join-Path $root 'src\AntiPilot\AntiPilot.csproj') `
        -c Release -r "win-$arch" --self-contained true `
        -p:Version=$assemblyVersion -p:DebugType=none `
        -o $publishDir --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $arch." }

    # --- stage --------------------------------------------------------------

    Write-Host "Staging package layout..." -ForegroundColor Cyan
    $stageDir = Join-Path $stageRoot $arch
    if (Test-Path $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
    New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
    Copy-Item (Join-Path $publishDir '*') $stageDir -Recurse -Force
    Get-ChildItem $stageDir -Filter '*.pdb' -Recurse | Remove-Item -Force

    # The logos travel with the publish output (see the Content item in AntiPilot.csproj).
    $logoCount = (Get-ChildItem (Join-Path $stageDir 'Images') -Filter '*.png' -ErrorAction SilentlyContinue | Measure-Object).Count
    if ($logoCount -eq 0) { throw "No logos in the staged Images folder." }
    Write-Host "Logos: $logoCount images" -ForegroundColor DarkGray

    Copy-Item (Join-Path $root 'packaging\Public') $stageDir -Recurse -Force

    # Edit the manifest as XML rather than by search-and-replace: the identity has to be exact.
    $manifest = [xml](Get-Content (Join-Path $root 'packaging\AppxManifest.xml') -Raw)
    $manifest.Package.Identity.Name = $identityName
    $manifest.Package.Identity.Publisher = $identityPublisher
    $manifest.Package.Identity.Version = $Version
    $manifest.Package.Identity.ProcessorArchitecture = $arch
    $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
    $manifest.Save((Join-Path $stageDir 'AppxManifest.xml'))

    # --- resource index -----------------------------------------------------

    Write-Host "Indexing resources..." -ForegroundColor Cyan
    Invoke-Tool $makepri @('new', '/pr', $stageDir, '/cf', $priConfig, '/of', (Join-Path $stageDir 'resources.pri'), '/o')

    # --- pack ---------------------------------------------------------------

    Write-Host "Packing MSIX..." -ForegroundColor Cyan
    $archPackage = Join-Path $packagesDir "AntiPilot-$arch.msix"
    Invoke-Tool $makeappx @('pack', '/d', $stageDir, '/p', $archPackage, '/o')
}

# --- bundle -----------------------------------------------------------------

Write-Host ""
if ($bundle) {
    Write-Host "Bundling $($Architectures.Count) architectures..." -ForegroundColor Cyan
    Invoke-Tool $makeappx @('bundle', '/d', $packagesDir, '/p', $artifactPath, '/bv', $Version, '/o')
}
else {
    Copy-Item (Join-Path $packagesDir "AntiPilot-$($Architectures[0]).msix") $artifactPath -Force
}

# --- sign -------------------------------------------------------------------

if ($SkipSign) {
    Write-Host ""
    if ($store) {
        Write-Host "Store package ready (unsigned, as Partner Center expects)." -ForegroundColor Green
        Write-Host "  Package: $artifactPath"
        Write-Host ""
        Write-Host "Upload it under Packages in your Partner Center submission." -ForegroundColor Cyan
        Write-Host "See packaging\store-listing.md for the listing text." -ForegroundColor Cyan
    }
    else {
        Write-Host "Skipping signing (package cannot be installed)." -ForegroundColor Yellow
        Write-Host "  Package: $artifactPath"
    }

    return
}

Write-Host "Signing..." -ForegroundColor Cyan

# The certificate subject has to equal the Identity/Publisher in the manifest, so a Store-identity
# package needs a certificate issued to the Store publisher GUID.
$signingSubject = $identityPublisher
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $signingSubject -and $_.NotAfter -gt (Get-Date) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "  Creating a self-signed code signing certificate for $signingSubject" -ForegroundColor DarkGray
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $signingSubject `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -FriendlyName 'AntiPilot self-signed package signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}Subject Type:End Entity')
}

Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null

$signtool = Find-SdkTool 'signtool.exe'
Invoke-Tool $signtool @('sign', '/fd', 'SHA256', '/a', '/sha1', $cert.Thumbprint, $artifactPath)

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Package:     $artifactPath"
Write-Host "  Certificate: $cerPath"
Write-Host ""

if ($store) {
    Write-Host "This is the Store-identity package SIGNED FOR TESTING - do not upload it." -ForegroundColor Yellow
    Write-Host "Rebuild without -SignForTesting to get the package Partner Center wants." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Next: run install.ps1 from an elevated PowerShell to trust the certificate and install." -ForegroundColor Cyan
Write-Host "  .\install.ps1 -Msix `"$artifactPath`"" -ForegroundColor DarkGray
