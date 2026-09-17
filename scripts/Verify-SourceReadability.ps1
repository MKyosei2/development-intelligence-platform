#requires -Version 5.1
# Purpose: Audits first-party source readability without exempting files solely because they contain license headers.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$ReportPath,
    [string]$InventoryPath,
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92, 47))
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if (-not $ReportPath) { $ReportPath = Join-Path $Repo 'artifacts\source-readability\report.md' }
if (-not $InventoryPath) { $InventoryPath = Join-Path $Repo 'artifacts\source-readability\inventory.csv' }

$Issues = New-Object 'System.Collections.Generic.List[object]'
$Inventory = New-Object 'System.Collections.Generic.List[object]'
$FilesScanned = 0

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    return ($full.Substring($Repo.Length) -replace '^[\\/]+', '').Replace([char]92, [char]47)
}

function Add-Issue {
    param(
        [ValidateSet('ERROR','WARN','INFO')][string]$Severity,
        [string]$Code,
        [string]$Path,
        [string]$Message,
        [int]$Line = 0
    )
    $Issues.Add([pscustomobject]@{
        Severity = $Severity
        Code = $Code
        Path = $Path
        Line = $Line
        Message = $Message
    }) | Out-Null
}

function Test-ExcludedPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $rel = (Get-RelativePath $Path).Replace([char]47, [char]92)
    if ($rel -match '(^|\\)(\.git|bin|obj|node_modules|packages|vendor|third_party|ThirdParty|Intermediate|Binaries|Library|Temp|Logs|Migrations|Generated|generated)(\\|$)') {
        return $true
    }
    $name = [System.IO.Path]::GetFileName($Path)
    return $name -match '\.(g|generated|designer)\.cs$' -or
        $name -in @('AssemblyInfo.cs', 'GlobalUsings.g.cs')
}

function Get-PurposeMatches {
    param([string]$Text)
    return @([regex]::Matches($Text, '(?m)^\s*(?://|#)\s*Purpose\s*:\s*(.+?)\s*$'))
}

function Test-BrokenPurposeText {
    param([string]$Purpose, [string]$FilePath)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    $normalized = $stem -replace '[^A-Za-z0-9]', ''
    if ($normalized.Length -lt 4) { return $false }
    $tokens = @([regex]::Matches($Purpose, '[A-Za-z0-9]+') | ForEach-Object { $_.Value })
    for ($start = 0; $start -lt $tokens.Count; $start++) {
        $joined = ''
        $short = 0
        for ($end = $start; $end -lt $tokens.Count -and $end -lt ($start + 32); $end++) {
            $joined += $tokens[$end]
            if ($tokens[$end].Length -le 2) { $short++ }
            if ($joined.Length -gt $normalized.Length) { break }
            if (($end - $start + 1) -ge 3 -and $short -ge 2 -and
                $joined.Equals($normalized, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
    }
    return $false
}

function Get-CompressedCSharpLines {
    param([string[]]$Lines)
    $findings = New-Object 'System.Collections.Generic.List[object]'
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        if ($line.Length -lt 180) { continue }
        $semicolons = [regex]::Matches($line, ';').Count
        $joinedBlocks = $line -match '}\s*(?:else\s+)?(?:if|for|foreach|while|switch|try|catch|finally|public|private|internal|protected)\b'
        $controls = [regex]::Matches($line, '\b(?:if|for|foreach|while|switch)\s*\(').Count -ge 2
        $assignments = [regex]::Matches($line, '(?<![=!<>])=(?!=|>)').Count -ge 3
        if ($semicolons -ge 3 -or $joinedBlocks -or $controls -or ($line.Length -ge 320 -and $assignments)) {
            $findings.Add([pscustomobject]@{ Line = $i + 1; Length = $line.Length }) | Out-Null
        }
    }
    return @($findings)
}

$extensions = @('.cs', '.ps1', '.py', '.cpp', '.c', '.h', '.hpp', '.js', '.ts', '.tsx', '.jsx')
foreach ($rootName in @('src', 'tests', 'scripts', 'services', 'collectors', 'plugins')) {
    $root = Join-Path $Repo $rootName
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }

    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        if (Test-ExcludedPath $file.FullName) { continue }
        $extension = $file.Extension.ToLowerInvariant()
        if ($extension -notin $extensions) { continue }

        $text = [System.IO.File]::ReadAllText($file.FullName)
        if ([string]::IsNullOrEmpty($text)) { continue }

        $FilesScanned++
        $relative = Get-RelativePath $file.FullName
        $lines = @(($text -replace "`r`n","`n" -replace "`r","`n") -split "`n")
        $purposeMatches = Get-PurposeMatches $text
        $purpose = ''
        $broken = $false

        if ($purposeMatches.Count -eq 0) {
            Add-Issue ERROR MISSING_PURPOSE $relative 'Handwritten first-party source has no Purpose comment.'
        }
        else {
            $purpose = $purposeMatches[0].Groups[1].Value.Trim()
            if ($purposeMatches.Count -gt 1) {
                Add-Issue ERROR DUPLICATE_PURPOSE $relative "Found $($purposeMatches.Count) Purpose comments; exactly one is allowed."
            }
            if ($purpose.Length -lt 12 -or $purpose.Length -gt 180) {
                Add-Issue WARN PURPOSE_LENGTH $relative "Purpose length is $($purpose.Length); expected 12-180 characters."
            }
            if (Test-BrokenPurposeText $purpose $file.FullName) {
                $broken = $true
                Add-Issue ERROR BROKEN_PURPOSE $relative 'Purpose contains character-split filename/type text.'
            }
        }

        $compressed = @()
        if ($extension -eq '.cs') {
            $compressed = @(Get-CompressedCSharpLines $lines)
            foreach ($finding in $compressed) {
                Add-Issue ERROR COMPRESSED_CSHARP $relative "Line $($finding.Line) contains compressed statements." $finding.Line
            }
        }

        $maxLength = if ($lines.Count -gt 0) {
            ($lines | ForEach-Object { $_.Length } | Measure-Object -Maximum).Maximum
        } else { 0 }

        $Inventory.Add([pscustomobject]@{
            Path = $relative
            Extension = $extension
            Lines = $lines.Count
            PurposeCount = $purposeMatches.Count
            Purpose = $purpose
            BrokenPurpose = $broken
            CompressedCSharpLines = $compressed.Count
            MaxLineLength = $maxLength
        }) | Out-Null
    }
}

$featureRoot = Join-Path $Repo 'src\Dip.Application\Features'
$referenceRoot = Join-Path $Repo 'docs\code-reference'
if (Test-Path -LiteralPath $featureRoot -PathType Container) {
    foreach ($directory in Get-ChildItem -LiteralPath $featureRoot -Directory | Sort-Object Name) {
        if (@(Get-ChildItem -LiteralPath $directory.FullName -Recurse -File -Filter '*.cs').Count -eq 0) { continue }
        $readme = Join-Path (Join-Path $referenceRoot $directory.Name) 'README.md'
        if (-not (Test-Path -LiteralPath $readme -PathType Leaf)) {
            Add-Issue ERROR MISSING_FEATURE_REFERENCE "docs/code-reference/$($directory.Name)/README.md" "Feature '$($directory.Name)' has source code but no Japanese code-reference README."
        }
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $referenceRoot 'README.md') -PathType Leaf)) {
    Add-Issue ERROR MISSING_REFERENCE_INDEX 'docs/code-reference/README.md' 'Japanese code-reference index is missing.'
}

foreach ($path in @($ReportPath, $InventoryPath)) {
    $parent = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
}
$Inventory | Sort-Object Path | Export-Csv -LiteralPath $InventoryPath -NoTypeInformation -Encoding UTF8

$errors = @($Issues | Where-Object Severity -eq 'ERROR').Count
$warnings = @($Issues | Where-Object Severity -eq 'WARN').Count
$total = $Issues.Count

$report = New-Object 'System.Collections.Generic.List[string]'
$report.Add('# Source Cleanup Report') | Out-Null
$report.Add('') | Out-Null
$report.Add("- Generated (UTC): $([DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))") | Out-Null
$report.Add('- Audit: `source-readability-v4`') | Out-Null
$report.Add('- License-header policy: first-party files are audited; only explicit generated/vendor/third-party paths are excluded.') | Out-Null
$report.Add("- Files scanned: $FilesScanned") | Out-Null
$report.Add("- Readability/documentation findings: **$total**") | Out-Null
$report.Add("- Errors: **$errors**") | Out-Null
$report.Add("- Warnings: **$warnings**") | Out-Null
$report.Add('') | Out-Null
$report.Add('## Findings') | Out-Null
$report.Add('') | Out-Null
$report.Add('| Severity | Code | Path | Line | Detail |') | Out-Null
$report.Add('|---|---|---|---:|---|') | Out-Null

if ($total -eq 0) {
    $report.Add('| PASS | NONE | - | - | No findings. |') | Out-Null
}
else {
    foreach ($issue in $Issues | Sort-Object Code, Path, Line) {
        $line = if ($issue.Line -gt 0) { [string]$issue.Line } else { '-' }
        $detail = $issue.Message.Replace('|', '\|')
        $report.Add("| $($issue.Severity) | $($issue.Code) | ``$($issue.Path)`` | $line | $detail |") | Out-Null
    }
}

[System.IO.File]::WriteAllText($ReportPath, (($report -join "`n") + "`n"), $Utf8NoBom)

Write-Host "Source readability audit: files=$FilesScanned findings=$total errors=$errors warnings=$warnings" -ForegroundColor Cyan
Write-Host "Report: $ReportPath"
Write-Host "Inventory: $InventoryPath"

if ($Strict -and $total -gt 0) {
    throw "Source readability audit failed with $total finding(s). See $ReportPath."
}
if (-not $Strict -and $errors -gt 0) {
    throw "Source readability audit found $errors error(s). See $ReportPath."
}
