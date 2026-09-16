#requires -Version 5.1
# Purpose: Audits Purpose quality, compressed C# statements, and Feature documentation coverage without treating mere comment presence as a pass.
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

if (-not $ReportPath) {
    $ReportPath = Join-Path $Repo 'docs\code-guide\_generated\source-cleanup-report.md'
}
if (-not $InventoryPath) {
    $InventoryPath = Join-Path $Repo 'docs\code-guide\_generated\source-readability-inventory.csv'
}

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
        [Parameter(Mandatory = $true)][ValidateSet('ERROR', 'WARN', 'INFO')][string]$Severity,
        [Parameter(Mandatory = $true)][string]$Code,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Message,
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
    if ($name -match '\.(g|generated|designer)\.cs$') {
        return $true
    }

    if ($name -in @('AssemblyInfo.cs', 'GlobalUsings.g.cs')) {
        return $true
    }

    return $false
}

function Test-LicenseHeader {
    param([Parameter(Mandatory = $true)][string]$Text)
    $head = $Text.Substring(0, [Math]::Min(2500, $Text.Length))
    return $head -match '(?im)Copyright|SPDX-License-Identifier|Licensed under'
}

function Get-PurposeMatches {
    param([Parameter(Mandatory = $true)][string]$Text)
    return @([regex]::Matches($Text, '(?m)^\s*(?://|#)\s*Purpose\s*:\s*(.+?)\s*$'))
}

function Test-BrokenPurposeText {
    param(
        [Parameter(Mandatory = $true)][string]$Purpose,
        [Parameter(Mandatory = $true)][string]$FilePath
    )

    $stem = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    if ($stem.EndsWith('.xaml', [System.StringComparison]::OrdinalIgnoreCase)) {
        $stem = $stem.Substring(0, $stem.Length - 5)
    }
    $stemNormalized = ($stem -replace '[^A-Za-z0-9]', '')
    if ($stemNormalized.Length -lt 4) {
        return $false
    }

    $tokens = @([regex]::Matches($Purpose, '[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*') | ForEach-Object { $_.Value })
    $run = ''
    $runCount = 0
    foreach ($token in $tokens) {
        $normalized = ($token -replace '[^A-Za-z0-9]', '')
        if ($normalized.Length -gt 0 -and $normalized.Length -le 2) {
            $run += $normalized
            $runCount++
            if ($runCount -ge 4 -and $run.Length -ge 6) {
                if ($stemNormalized.IndexOf($run, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    return $true
                }
            }
        }
        else {
            $run = ''
            $runCount = 0
        }
    }

    return $false
}

function Get-CompressedCSharpLines {
    param([Parameter(Mandatory = $true)][string[]]$Lines)
    $findings = New-Object 'System.Collections.Generic.List[object]'

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        if ($line.Length -lt 180) {
            continue
        }

        $semicolonCount = [regex]::Matches($line, ';').Count
        $hasStatementJoin = $line -match '}\s*(?:else\s+)?(?:if|for|foreach|while|switch|try|catch|finally|public|private|internal|protected)\b'
        $hasMultipleControlBlocks = [regex]::Matches($line, '\b(?:if|for|foreach|while|switch)\s*\(').Count -ge 2
        $hasMultipleAssignments = [regex]::Matches($line, '(?<![=!<>])=(?!=|>)').Count -ge 3

        if (($semicolonCount -ge 3) -or $hasStatementJoin -or $hasMultipleControlBlocks -or ($line.Length -ge 320 -and $hasMultipleAssignments)) {
            $findings.Add([pscustomobject]@{
                Line = $i + 1
                Length = $line.Length
                Semicolons = $semicolonCount
            }) | Out-Null
        }
    }

    return @($findings)
}

$extensions = @('.cs', '.ps1', '.py', '.cpp', '.c', '.h', '.hpp', '.js', '.ts', '.tsx', '.jsx')
foreach ($rootName in @('src', 'tests', 'scripts', 'services', 'collectors', 'plugins')) {
    $root = Join-Path $Repo $rootName
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        continue
    }

    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        if (Test-ExcludedPath -Path $file.FullName) {
            continue
        }

        $extension = $file.Extension.ToLowerInvariant()
        if ($extension -notin $extensions) {
            continue
        }

        $text = [System.IO.File]::ReadAllText($file.FullName)
        if ([string]::IsNullOrEmpty($text)) {
            continue
        }
        if (Test-LicenseHeader -Text $text) {
            continue
        }

        $FilesScanned++
        $relative = Get-RelativePath $file.FullName
        $normalizedText = $text -replace "`r`n", "`n" -replace "`r", "`n"
        $lines = @($normalizedText -split "`n")
        $purposeMatches = Get-PurposeMatches -Text $text
        $purpose = ''
        $brokenPurpose = $false

        if ($purposeMatches.Count -eq 0) {
            Add-Issue -Severity 'ERROR' -Code 'MISSING_PURPOSE' -Path $relative -Message 'Handwritten source has no Purpose comment.'
        }
        else {
            $purpose = $purposeMatches[0].Groups[1].Value.Trim()
            if ($purposeMatches.Count -gt 1) {
                Add-Issue -Severity 'ERROR' -Code 'DUPLICATE_PURPOSE' -Path $relative -Message "Found $($purposeMatches.Count) Purpose comments; exactly one is allowed."
            }
            if ($purpose.Length -lt 12 -or $purpose.Length -gt 180) {
                Add-Issue -Severity 'WARN' -Code 'PURPOSE_LENGTH' -Path $relative -Message "Purpose length is $($purpose.Length); expected 12-180 characters."
            }
            if (Test-BrokenPurposeText -Purpose $purpose -FilePath $file.FullName) {
                $brokenPurpose = $true
                Add-Issue -Severity 'ERROR' -Code 'BROKEN_PURPOSE' -Path $relative -Message 'Purpose contains character-split filename/type text and is not human-readable.'
            }
        }

        $compressed = @()
        if ($extension -eq '.cs') {
            $compressed = @(Get-CompressedCSharpLines -Lines $lines)
            foreach ($finding in $compressed) {
                Add-Issue -Severity 'ERROR' -Code 'COMPRESSED_CSHARP' -Path $relative -Line $finding.Line -Message "Line $($finding.Line) contains multiple compressed statements ($($finding.Length) characters)."
            }
        }

        $maxLineLength = 0
        if ($lines.Count -gt 0) {
            $maxLineLength = ($lines | ForEach-Object { $_.Length } | Measure-Object -Maximum).Maximum
        }

        $Inventory.Add([pscustomobject]@{
            Path = $relative
            Extension = $extension
            Lines = $lines.Count
            PurposeCount = $purposeMatches.Count
            Purpose = $purpose
            BrokenPurpose = $brokenPurpose
            CompressedCSharpLines = $compressed.Count
            MaxLineLength = $maxLineLength
        }) | Out-Null
    }
}

$featureRoot = Join-Path $Repo 'src\Dip.Application\Features'
$referenceRoot = Join-Path $Repo 'docs\code-reference'
if (Test-Path -LiteralPath $featureRoot -PathType Container) {
    foreach ($featureDirectory in Get-ChildItem -LiteralPath $featureRoot -Directory | Sort-Object Name) {
        $featureReadme = Join-Path (Join-Path $referenceRoot $featureDirectory.Name) 'README.md'
        if (-not (Test-Path -LiteralPath $featureReadme -PathType Leaf)) {
            Add-Issue -Severity 'ERROR' -Code 'MISSING_FEATURE_REFERENCE' -Path "docs/code-reference/$($featureDirectory.Name)/README.md" -Message "Feature '$($featureDirectory.Name)' has source code but no Japanese code-reference README."
        }
    }
}

$referenceIndex = Join-Path $referenceRoot 'README.md'
if (-not (Test-Path -LiteralPath $referenceIndex -PathType Leaf)) {
    Add-Issue -Severity 'ERROR' -Code 'MISSING_REFERENCE_INDEX' -Path 'docs/code-reference/README.md' -Message 'Japanese code-reference index is missing.'
}

$reportParent = Split-Path -Parent $ReportPath
if (-not (Test-Path -LiteralPath $reportParent -PathType Container)) {
    New-Item -ItemType Directory -Path $reportParent -Force | Out-Null
}

$inventoryParent = Split-Path -Parent $InventoryPath
if (-not (Test-Path -LiteralPath $inventoryParent -PathType Container)) {
    New-Item -ItemType Directory -Path $inventoryParent -Force | Out-Null
}
$Inventory | Sort-Object Path | Export-Csv -LiteralPath $InventoryPath -NoTypeInformation -Encoding UTF8

$errors = @($Issues | Where-Object { $_.Severity -eq 'ERROR' }).Count
$warnings = @($Issues | Where-Object { $_.Severity -eq 'WARN' }).Count
$infos = @($Issues | Where-Object { $_.Severity -eq 'INFO' }).Count
$brokenCount = @($Issues | Where-Object { $_.Code -eq 'BROKEN_PURPOSE' }).Count
$compressedCount = @($Issues | Where-Object { $_.Code -eq 'COMPRESSED_CSHARP' }).Count
$missingReferenceCount = @($Issues | Where-Object { $_.Code -eq 'MISSING_FEATURE_REFERENCE' }).Count
$total = $Issues.Count
$generatedUtc = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')

$report = New-Object 'System.Collections.Generic.List[string]'
$report.Add('# Source Cleanup Report') | Out-Null
$report.Add('') | Out-Null
$report.Add("- Generated (UTC): $generatedUtc") | Out-Null
$report.Add('- Audit: `source-readability-v2`') | Out-Null
$report.Add("- Files scanned: $FilesScanned") | Out-Null
$report.Add("- Readability/documentation findings: **$total**") | Out-Null
$report.Add("- Errors: **$errors**") | Out-Null
$report.Add("- Warnings: **$warnings**") | Out-Null
$report.Add("- Broken Purpose comments: **$brokenCount**") | Out-Null
$report.Add("- Compressed C# lines: **$compressedCount**") | Out-Null
$report.Add("- Missing Feature references: **$missingReferenceCount**") | Out-Null
$report.Add('') | Out-Null
$report.Add('## Pass condition') | Out-Null
$report.Add('') | Out-Null
$report.Add('This report is clean only when **Readability/documentation findings = 0**. A Purpose comment merely existing is not sufficient: malformed character-split text, duplicates, compressed C# statements, and missing Feature documentation are separate failures.') | Out-Null
$report.Add('') | Out-Null
$report.Add('## Checks') | Out-Null
$report.Add('') | Out-Null
$report.Add('| Check | Count |') | Out-Null
$report.Add('|---|---:|') | Out-Null
foreach ($code in @('MISSING_PURPOSE', 'DUPLICATE_PURPOSE', 'BROKEN_PURPOSE', 'PURPOSE_LENGTH', 'COMPRESSED_CSHARP', 'MISSING_FEATURE_REFERENCE', 'MISSING_REFERENCE_INDEX')) {
    $count = @($Issues | Where-Object { $_.Code -eq $code }).Count
    $report.Add("| $code | $count |") | Out-Null
}
$report.Add('') | Out-Null
$report.Add('## Findings') | Out-Null
$report.Add('') | Out-Null
$report.Add('| Severity | Code | Path | Line | Detail |') | Out-Null
$report.Add('|---|---|---|---:|---|') | Out-Null
if ($total -eq 0) {
    $report.Add('| PASS | NONE | - | - | No static readability or Feature-documentation findings. |') | Out-Null
}
else {
    foreach ($issue in $Issues | Sort-Object @{ Expression = { switch ($_.Severity) { 'ERROR' { 0 } 'WARN' { 1 } default { 2 } } } }, Code, Path, Line) {
        $lineText = if ($issue.Line -gt 0) { [string]$issue.Line } else { '-' }
        $detail = $issue.Message.Replace('|', '\|')
        $report.Add("| $($issue.Severity) | $($issue.Code) | ``$($issue.Path)`` | $lineText | $detail |") | Out-Null
    }
}
$report.Add('') | Out-Null
$report.Add('## Re-run') | Out-Null
$report.Add('') | Out-Null
$report.Add('```powershell') | Out-Null
$report.Add('.\scripts\Verify-SourceReadability.ps1 -Strict') | Out-Null
$report.Add('```') | Out-Null

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
