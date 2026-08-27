<#
.SYNOPSIS
    Fills a Partner Center listing export with the copy in packaging\listing, ready to re-import.

.DESCRIPTION
    Partner Center lets you export the whole listing as a CSV of Field/ID/Type rows with one column
    per locale, edit it, and import it back. This script rewrites that CSV from the per-language
    files in packaging\listing, leaving every row, ID and column exactly where Partner Center put
    them — only the cells change, so the import is accepted.

    Two things about that CSV bite anyone scripting against it:

      * Import-Csv cannot read it. The header has both "ID" (the field id) and "id" (Indonesian),
        and PowerShell treats object members case-insensitively, so it throws "The member id is
        already present". Everything here is done positionally instead.
      * The Description and ReleaseNotes cells contain newlines, so the file must be parsed and
        written as real CSV with quoting, not split on line breaks.

.PARAMETER Export
    The CSV downloaded from Partner Center (Store listings > Export/Import listings).

.PARAMETER Out
    Where to write the filled CSV. Defaults to <Export> with "-filled" appended.

.PARAMETER ListingDir
    Where the per-language copy lives.

.EXAMPLE
    .\tools\Update-Listing.ps1 -Export ~\Downloads\listingData-9N4S7TXSMP3P-1152921505701697530.csv
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Export,

    [string]$Out,

    [string]$ListingDir = (Join-Path $PSScriptRoot '..\packaging\listing')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName Microsoft.VisualBasic

# Partner Center's own limits. Anything longer is rejected at import, so it is caught here instead.
$limits = @{
    ShortDescription              = 1000
    Description                   = 10000
    ReleaseNotes                  = 1500
    CopyrightTrademarkInformation = 200
    Title                         = 256
}
$featureLimit = 200
$captionLimit = 200
$searchTermLimit = 40
$searchTermWordBudget = 21

function Read-Listing([string]$path) {
    $fields = [ordered]@{}
    $current = $null
    $buffer = [Collections.Generic.List[string]]::new()

    foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
        if ($line -match '^\[(?<name>[A-Za-z0-9]+)\]\s*$') {
            if ($current) { $fields[$current] = ($buffer -join "`n").Trim() }
            $current = $Matches.name
            $buffer.Clear()
            continue
        }

        # A # line is always a comment, wherever it appears. Treating them as comments only
        # outside a field seemed more careful, but it silently appended the notes between two
        # fields onto the end of the first one — the limit check below is what caught it.
        # No Store copy legitimately starts a line with #.
        if ($line.TrimStart().StartsWith('#')) { continue }
        if ($current) { $buffer.Add($line) }
    }

    if ($current) { $fields[$current] = ($buffer -join "`n").Trim() }
    return $fields
}

function Test-Limits([string]$tag, $fields) {
    $problems = [Collections.Generic.List[string]]::new()

    foreach ($name in $fields.Keys) {
        $value = $fields[$name]
        $limit = if ($limits.ContainsKey($name)) { $limits[$name] }
                 elseif ($name -like 'Feature*') { $featureLimit }
                 elseif ($name -like '*Caption*') { $captionLimit }
                 elseif ($name -like 'SearchTerm*') { $searchTermLimit }
                 else { $null }

        if ($limit -and $value.Length -gt $limit) {
            $problems.Add("$tag/${name}: $($value.Length) characters, limit $limit")
        }
    }

    $terms = @($fields.Keys | Where-Object { $_ -like 'SearchTerm*' } | ForEach-Object { $fields[$_] })
    $words = ($terms -join ' ' -split '\s+' | Where-Object { $_ }).Count
    if ($words -gt $searchTermWordBudget) {
        $problems.Add("$tag/SearchTerms: $words words across all terms, limit $searchTermWordBudget")
    }

    # A character from a script this language does not use is almost always a slip rather than a
    # choice, and it is nearly invisible in a wall of text you cannot read. One did survive review
    # in the Russian copy — a lone CJK character sitting inside a Cyrillic word.
    $foreign = switch -Wildcard ($tag) {
        'zh*'   { $null }                                        # Han is the script
        'ja'    { $null }                                        # Han plus kana
        'ko'    { '[⺀-鿿]' }                            # Hangul yes, Han no
        default { '[⺀-鿿가-힯぀-ヿ]' }  # no Han, Hangul or kana at all
    }

    if ($foreign) {
        foreach ($name in $fields.Keys) {
            $hits = [regex]::Matches($fields[$name], $foreign)
            if ($hits.Count -gt 0) {
                $shown = ($hits | Select-Object -First 5 | ForEach-Object { "U+{0:X4} {1}" -f [int][char]$_.Value[0], $_.Value }) -join ', '
                $problems.Add("$tag/${name}: $($hits.Count) character(s) from another script: $shown")
            }
        }
    }

    return $problems
}

# ---- read the export ---------------------------------------------------------

$parser = New-Object Microsoft.VisualBasic.FileIO.TextFieldParser($Export)
$parser.TextFieldType = 'Delimited'
$parser.SetDelimiters(',')
$parser.HasFieldsEnclosedInQuotes = $true

$rows = [Collections.Generic.List[string[]]]::new()
while (-not $parser.EndOfData) { $rows.Add($parser.ReadFields()) }
$parser.Close()

if ($rows.Count -eq 0) { throw "$Export is empty." }

$header = $rows[0]
$width = $header.Count
$localeColumn = @{}
for ($i = 4; $i -lt $width; $i++) { $localeColumn[$header[$i]] = $i }

Write-Host "Locales in the export: $($localeColumn.Keys -join ', ')" -ForegroundColor Cyan

# ---- load the copy -----------------------------------------------------------

# Partner Center writes locale columns lower case and hyphenated (zh-hans); the files are named
# the way the app names its languages (zh-Hans), so match case-insensitively.
$copy = @{}
foreach ($file in Get-ChildItem -LiteralPath $ListingDir -Filter '*.txt') {
    $tag = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    $column = $localeColumn.Keys | Where-Object { $_ -eq $tag -or $_ -eq "$tag-us" -or $_ -replace '-','' -eq ($tag -replace '-','') } | Select-Object -First 1
    if (-not $column) {
        Write-Warning "packaging\listing\$($file.Name) has no matching column in the export; skipped."
        continue
    }

    $copy[$column] = Read-Listing $file.FullName
}

$problems = [Collections.Generic.List[string]]::new()
foreach ($column in $copy.Keys) { Test-Limits $column $copy[$column] | ForEach-Object { $problems.Add($_) } }

if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    throw "$($problems.Count) field(s) exceed the Partner Center limits."
}

# ---- fill ---------------------------------------------------------------------

# Which screenshot slots actually hold an image, per locale. A caption for a slot with no image
# behind it is noise in the listing, and Partner Center shows it against nothing.
$hasImage = @{}
foreach ($row in $rows) {
    if ($row.Count -lt $width) { continue }
    if ($row[0] -notmatch '^DesktopScreenshot(\d+)$') { continue }

    $slot = $Matches[1]
    foreach ($column in $localeColumn.Keys) {
        if (-not [string]::IsNullOrWhiteSpace($row[$localeColumn[$column]])) {
            $hasImage["$column/$slot"] = $true
        }
    }
}

$filled = 0
$skipped = 0
foreach ($row in $rows) {
    if ($row.Count -lt $width) { continue }
    $field = $row[0]

    foreach ($column in $copy.Keys) {
        $fields = $copy[$column]
        if (-not $fields.Contains($field)) { continue }

        if ($field -match '^DesktopScreenshotCaption(\d+)$' -and -not $hasImage["$column/$($Matches[1])"]) {
            $skipped++
            continue
        }

        $row[$localeColumn[$column]] = $fields[$field]
        $filled++
    }
}

if ($skipped -gt 0) {
    Write-Host "Skipped $skipped caption(s) with no screenshot in that slot." -ForegroundColor DarkGray
}

# ---- write --------------------------------------------------------------------

function ConvertTo-CsvField([string]$value) {
    if ($null -eq $value) { return '' }
    if ($value -match '[",\r\n]') { return '"' + ($value -replace '"', '""') + '"' }
    return $value
}

if (-not $Out) {
    $Out = [IO.Path]::Combine(
        [IO.Path]::GetDirectoryName((Resolve-Path $Export)),
        [IO.Path]::GetFileNameWithoutExtension($Export) + '-filled.csv')
}

$builder = [Text.StringBuilder]::new()
foreach ($row in $rows) {
    [void]$builder.AppendLine((($row | ForEach-Object { ConvertTo-CsvField $_ }) -join ','))
}

# UTF-8 with a BOM: without one, Partner Center reads the non-Latin columns as mojibake.
[IO.File]::WriteAllText($Out, $builder.ToString(), [Text.UTF8Encoding]::new($true))

Write-Host "Filled $filled cells across $($copy.Count) locale(s)." -ForegroundColor Green
Write-Host "Wrote $Out"
Write-Host "Import it under Store listings > Import listings in Partner Center."
