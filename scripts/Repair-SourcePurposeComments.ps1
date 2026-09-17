#requires -Version 5.1
# Purpose: Repairs missing, duplicate, malformed, and unreadable Purpose comments in first-party handwritten source.
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$WhatIfReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92, 47))
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$Changed = New-Object 'System.Collections.Generic.List[string]'

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    return ($full.Substring($Repo.Length) -replace '^[\\/]+', '').Replace([char]92, [char]47)
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

function ConvertTo-HumanName {
    param([Parameter(Mandatory = $true)][string]$Name)
    $value = $Name -replace '([a-z0-9])([A-Z])', '$1 $2'
    $value = $value -replace '([A-Z]+)([A-Z][a-z])', '$1 $2'
    return (($value -replace '[_\-]+', ' ') -replace '\s+', ' ').Trim()
}

function Get-DeclaredTypeNames {
    param([Parameter(Mandatory = $true)][string]$Content)
    $pattern = '(?m)^(?:public|internal)\s+(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record(?:\s+(?:class|struct))?|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)'
    return @(
        [regex]::Matches($Content, $pattern) |
        ForEach-Object { $_.Groups[1].Value } |
        Select-Object -Unique
    )
}

function Test-FragmentedName {
    param(
        [Parameter(Mandatory = $true)][string]$Purpose,
        [Parameter(Mandatory = $true)][string]$Candidate
    )
    $candidateNormalized = $Candidate -replace '[^A-Za-z0-9]', ''
    if ($candidateNormalized.Length -lt 4) { return $false }
    $tokens = @([regex]::Matches($Purpose, '[A-Za-z0-9]+') | ForEach-Object { $_.Value })
    for ($start = 0; $start -lt $tokens.Count; $start++) {
        $joined = ''
        $shortCount = 0
        for ($end = $start; $end -lt $tokens.Count -and $end -lt ($start + 32); $end++) {
            $joined += $tokens[$end]
            if ($tokens[$end].Length -le 2) { $shortCount++ }
            if ($joined.Length -gt $candidateNormalized.Length) { break }
            $parts = $end - $start + 1
            if ($parts -ge 3 -and $shortCount -ge 2 -and
                $joined.Equals($candidateNormalized, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
    }
    return $false
}

function Test-BrokenPurpose {
    param(
        [Parameter(Mandatory = $true)][string]$Purpose,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Content
    )
    $candidates = New-Object 'System.Collections.Generic.List[string]'
    $candidates.Add([System.IO.Path]::GetFileNameWithoutExtension($FilePath)) | Out-Null
    foreach ($typeName in @(Get-DeclaredTypeNames -Content $Content)) {
        if (-not $candidates.Contains($typeName)) { $candidates.Add($typeName) | Out-Null }
    }
    foreach ($candidate in $candidates) {
        if (Test-FragmentedName -Purpose $Purpose -Candidate $candidate) { return $true }
    }
    return $false
}

function Get-ReplacementPurpose {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Content
    )
    $relative = Get-RelativePath $FilePath
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    $names = @(Get-DeclaredTypeNames -Content $Content)
    $name = if ($names.Count -eq 1) { $names[0] } else { $baseName }
    $human = ConvertTo-HumanName $name

    if ($baseName -eq 'Program') { return 'Composes and starts this DIP host.' }
    if ($relative -match '^tests/') { return "Verifies behavior covered by $human." }
    if ($relative -match '^scripts/') { return "Automates the repository workflow for $human." }
    if ($name -match 'Controller$') { return "Exposes HTTP endpoints implemented by $human." }
    if ($name -match '^I[A-Z].*Repository$') { return "Defines the persistence boundary represented by $human." }
    if ($name -match 'Repository$') { return "Persists and queries data through $human." }
    if ($name -match 'Worker$|Job$') { return "Runs background processing through $human." }
    if ($name -match 'Service$') { return "Orchestrates the workflow implemented by $human." }
    if ($name -match 'Dto$|Request$|Response$|Contract') { return "Defines data exchanged through $human." }
    if ($relative -match '^src/Dip\.Domain/') { return "Defines $human in the DIP domain." }
    if ($relative -match '^src/Dip\.Infrastructure/') { return "Provides the infrastructure responsibility represented by $human." }
    if ($relative -match '^src/Dip\.Web/') { return "Implements the web responsibility represented by $human." }
    return "Defines the DIP responsibility represented by $human."
}

function Get-Prefix {
    param([string]$Extension)
    if ($Extension -in @('.ps1', '.py')) { return '# Purpose: ' }
    return '// Purpose: '
}

function Get-InsertionIndex {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Extension
    )
    $index = 0
    if ($Extension -eq '.ps1') {
        while ($index -lt $Lines.Count -and $Lines[$index] -match '^\s*#requires\b') { $index++ }
    }
    elseif ($Extension -eq '.py') {
        if ($index -lt $Lines.Count -and $Lines[$index] -match '^#!') { $index++ }
        if ($index -lt $Lines.Count -and $Lines[$index] -match '^#.*coding[:=]') { $index++ }
    }

    # Preserve a leading first-party license block; do not exempt the file from validation.
    while ($index -lt $Lines.Count -and
           ($Lines[$index] -match '^\s*(?://|#).*(Copyright|SPDX-License-Identifier|Licensed under)' -or
            ($index -gt 0 -and $Lines[$index] -match '^\s*(?://|#)'))) {
        $index++
    }
    return $index
}

$extensions = @('.cs', '.ps1', '.py', '.cpp', '.c', '.h', '.hpp', '.js', '.ts', '.tsx', '.jsx')
foreach ($rootName in @('src', 'tests', 'scripts', 'services', 'collectors', 'plugins')) {
    $root = Join-Path $Repo $rootName
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }

    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        if (Test-ExcludedPath $file.FullName) { continue }
        $extension = $file.Extension.ToLowerInvariant()
        if ($extension -notin $extensions) { continue }

        $content = [System.IO.File]::ReadAllText($file.FullName)
        if ([string]::IsNullOrEmpty($content)) { continue }

        $normalized = $content -replace "`r`n", "`n" -replace "`r", "`n"
        $lines = New-Object 'System.Collections.Generic.List[string]'
        foreach ($line in ($normalized -split "`n")) { $lines.Add($line) | Out-Null }

        $indexes = New-Object 'System.Collections.Generic.List[int]'
        for ($i = 0; $i -lt [Math]::Min(80, $lines.Count); $i++) {
            if ($lines[$i] -match '^\s*(?://|#)\s*Purpose\s*:\s*(.*?)\s*$') {
                $indexes.Add($i) | Out-Null
            }
        }

        $replacement = Get-ReplacementPurpose -FilePath $file.FullName -Content $content
        $prefix = Get-Prefix $extension
        $needsChange = $false

        if ($indexes.Count -eq 0) {
            $insertAt = Get-InsertionIndex -Lines $lines -Extension $extension
            $lines.Insert($insertAt, $prefix + $replacement)
            $needsChange = $true
        }
        else {
            $first = $indexes[0]
            $match = [regex]::Match($lines[$first], '^\s*(?://|#)\s*Purpose\s*:\s*(.*?)\s*$')
            $purpose = $match.Groups[1].Value.Trim()
            if ($purpose.Length -lt 12 -or $purpose.Length -gt 180 -or
                (Test-BrokenPurpose -Purpose $purpose -FilePath $file.FullName -Content $content)) {
                $lines[$first] = $prefix + $replacement
                $needsChange = $true
            }
            if ($indexes.Count -gt 1) {
                for ($j = $indexes.Count - 1; $j -ge 1; $j--) {
                    $lines.RemoveAt($indexes[$j])
                }
                $needsChange = $true
            }
        }

        if (-not $needsChange) { continue }
        $relative = Get-RelativePath $file.FullName
        if ($WhatIfReport -or $WhatIfPreference) {
            Write-Host "[WOULD FIX] $relative" -ForegroundColor Yellow
            continue
        }
        if ($PSCmdlet.ShouldProcess($relative, 'Normalize Purpose comment')) {
            [System.IO.File]::WriteAllText(
                $file.FullName,
                (($lines -join "`n").TrimEnd("`n") + "`n"),
                $Utf8NoBom)
            $Changed.Add($relative) | Out-Null
            Write-Host "[FIXED] $relative" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "Purpose normalization complete. Changed: $($Changed.Count)" -ForegroundColor Cyan
