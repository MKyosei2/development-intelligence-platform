#requires -Version 5.1
# Purpose: Repairs only malformed or duplicated Purpose comments without renaming files or changing application behavior.
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
$Skipped = New-Object 'System.Collections.Generic.List[string]'

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
    if ($name -match '\.(g|generated|designer)\.cs$') {
        return $true
    }

    if ($name -in @('AssemblyInfo.cs', 'GlobalUsings.g.cs')) {
        return $true
    }

    return $false
}

function ConvertTo-HumanName {
    param([Parameter(Mandatory = $true)][string]$Name)
    $value = $Name
    $value = $value -replace '([a-z0-9])([A-Z])', '$1 $2'
    $value = $value -replace '([A-Z]+)([A-Z][a-z])', '$1 $2'
    $value = $value -replace '[_\-]+', ' '
    $value = ($value -replace '\s+', ' ').Trim()
    return $value
}

function Get-PrimaryTypeName {
    param([Parameter(Mandatory = $true)][string]$Content)
    $pattern = '(?m)^(?:public|internal)\s+(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record(?:\s+(?:class|struct))?|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)'
    $matches = [regex]::Matches($Content, $pattern)
    if ($matches.Count -eq 1) {
        return $matches[0].Groups[1].Value
    }

    return $null
}

function Get-FeatureArea {
    param([Parameter(Mandatory = $true)][string]$RelativePath)
    $normalized = $RelativePath.Replace([char]92, [char]47)
    if ($normalized -match '/Features/([^/]+)/') {
        return $Matches[1]
    }
    if ($normalized -match '^src/Dip\.Application/Abstractions/') {
        return 'Application'
    }
    if ($normalized -match '^src/Dip\.Infrastructure/([^/]+)/') {
        return $Matches[1]
    }
    if ($normalized -match '^src/Dip\.Api/([^/]+)/') {
        return $Matches[1]
    }
    if ($normalized -match '^tests/([^/]+)/') {
        return $Matches[1]
    }
    return 'DIP'
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

function Get-ReplacementPurpose {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $relative = Get-RelativePath $FilePath
    switch ($relative) {
        'src/Dip.Application/Features/ChangeRisk/ChangeRiskIntelligenceService.cs' {
            return 'Assesses deterministic change risk from change size, test coverage, incidents, regressions, and cross-repository impact.'
        }
        'src/Dip.Application/Features/Management/ProjectManagementCommandCenterService.cs' {
            return 'Builds the project-management command-center read model from delivery, approval, RACI, and quality data.'
        }
        'scripts/format-all.ps1' {
            return 'Runs repository-wide formatting and lint verification.'
        }
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    if ($baseName.EndsWith('.xaml', [System.StringComparison]::OrdinalIgnoreCase)) {
        $baseName = $baseName.Substring(0, $baseName.Length - 5)
    }

    $primaryType = Get-PrimaryTypeName -Content $Content
    $name = if ($primaryType) { $primaryType } else { $baseName }
    $humanName = ConvertTo-HumanName -Name $name
    $feature = Get-FeatureArea -RelativePath $relative

    if ($relative -match '^tests/') {
        return "Verifies expected $feature behavior covered by $humanName."
    }
    if ($relative -match '^scripts/') {
        return "Automates the repository workflow for $humanName."
    }
    if ($baseName -eq 'Program') {
        return 'Composes and starts this DIP host.'
    }
    if ($baseName -eq 'GlobalUsings') {
        return 'Provides shared imports for this project.'
    }
    if ($name -match 'Controller$') {
        return "Exposes $feature API operations through $humanName."
    }
    if ($name -match 'Validator$') {
        return "Validates $feature inputs and invariants through $humanName."
    }
    if ($name -match '^I[A-Z].*Repository$') {
        return "Defines the $feature persistence boundary represented by $humanName."
    }
    if ($name -match 'Repository$') {
        return "Persists and queries $feature data through $humanName."
    }
    if ($name -match 'Command$') {
        return "Represents the state-changing $feature request $humanName."
    }
    if ($name -match 'Query$') {
        return "Represents the read-only $feature request $humanName."
    }
    if ($name -match 'Handler$') {
        return "Executes the $feature request handled by $humanName."
    }
    if ($name -match 'Factory$') {
        return "Creates $feature objects through $humanName."
    }
    if ($name -match 'Policy$') {
        return "Defines the $feature policy represented by $humanName."
    }
    if ($name -match 'Rule$') {
        return "Defines the $feature rule represented by $humanName."
    }
    if ($name -match 'Worker$|Job$') {
        return "Runs $feature background processing through $humanName."
    }
    if ($name -match 'Dto$|Request$|Response$|Contract') {
        return "Defines data exchanged by $feature workflows through $humanName."
    }
    if ($name -match 'Service$') {
        return "Orchestrates the $feature workflow implemented by $humanName."
    }
    if ($relative -match '^src/Dip\.Domain/') {
        return "Defines $humanName in the $feature domain."
    }
    if ($relative -match '^src/Dip\.Application/Abstractions/') {
        return "Defines the application boundary represented by $humanName."
    }
    if ($relative -match '^src/Dip\.Infrastructure/') {
        return "Provides the infrastructure responsibility represented by $humanName."
    }
    if ($relative -match '^src/Dip\.Web/') {
        return "Implements the web responsibility represented by $humanName."
    }
    if ($relative -match '^src/Dip\.Desktop/') {
        return "Implements the desktop responsibility represented by $humanName."
    }

    return "Defines the DIP responsibility represented by $humanName."
}

function Repair-FilePurpose {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    if (Test-ExcludedPath -Path $File.FullName) {
        return
    }

    $extension = $File.Extension.ToLowerInvariant()
    if ($extension -notin @('.cs', '.ps1', '.py', '.cpp', '.c', '.h', '.hpp', '.js', '.ts', '.tsx', '.jsx')) {
        return
    }

    $content = [System.IO.File]::ReadAllText($File.FullName)
    if ([string]::IsNullOrEmpty($content)) {
        return
    }

    if ($content.Substring(0, [Math]::Min(2500, $content.Length)) -match '(?im)Copyright|SPDX-License-Identifier|Licensed under') {
        return
    }

    $normalizedContent = $content -replace "`r`n", "`n" -replace "`r", "`n"
    $lines = New-Object 'System.Collections.Generic.List[string]'
    foreach ($line in ([regex]::Split($normalizedContent, "`n"))) {
        $lines.Add($line) | Out-Null
    }

    $purposeIndexes = New-Object 'System.Collections.Generic.List[int]'
    $scanCount = [Math]::Min(40, $lines.Count)
    for ($i = 0; $i -lt $scanCount; $i++) {
        if ($lines[$i] -match '^\s*(?://|#)\s*Purpose\s*:\s*(.+?)\s*$') {
            $purposeIndexes.Add($i) | Out-Null
        }
    }

    if ($purposeIndexes.Count -eq 0) {
        $Skipped.Add("$(Get-RelativePath $File.FullName): no Purpose comment") | Out-Null
        return
    }

    $firstIndex = $purposeIndexes[0]
    $match = [regex]::Match($lines[$firstIndex], '^\s*(?://|#)\s*Purpose\s*:\s*(.+?)\s*$')
    $currentPurpose = $match.Groups[1].Value.Trim()
    $needsRepair = Test-BrokenPurposeText -Purpose $currentPurpose -FilePath $File.FullName
    $hasDuplicates = $purposeIndexes.Count -gt 1

    if (-not $needsRepair -and -not $hasDuplicates) {
        return
    }

    $prefix = if ($extension -in @('.ps1', '.py')) { '# Purpose: ' } else { '// Purpose: ' }
    $replacement = Get-ReplacementPurpose -FilePath $File.FullName -Content $content
    $lines[$firstIndex] = $prefix + $replacement

    if ($hasDuplicates) {
        for ($j = $purposeIndexes.Count - 1; $j -ge 1; $j--) {
            $lines.RemoveAt($purposeIndexes[$j])
        }
    }

    $relative = Get-RelativePath $File.FullName
    if ($WhatIfReport -or $WhatIfPreference) {
        Write-Host "[WOULD FIX] $relative" -ForegroundColor Yellow
        return
    }

    if ($PSCmdlet.ShouldProcess($relative, 'Repair malformed/duplicate Purpose comment')) {
        $output = ($lines -join "`n").TrimEnd("`n") + "`n"
        [System.IO.File]::WriteAllText($File.FullName, $output, $Utf8NoBom)
        $Changed.Add($relative) | Out-Null
        Write-Host "[FIXED] $relative" -ForegroundColor Green
    }
}

foreach ($rootName in @('src', 'tests', 'scripts', 'services', 'collectors', 'plugins')) {
    $root = Join-Path $Repo $rootName
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        continue
    }

    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        Repair-FilePurpose -File $file
    }
}

Write-Host ''
Write-Host "Purpose repair complete. Changed: $($Changed.Count); skipped-without-purpose: $($Skipped.Count)" -ForegroundColor Cyan
if ($Changed.Count -gt 0) {
    Write-Host 'Changed files:' -ForegroundColor Cyan
    $Changed | Sort-Object | ForEach-Object { Write-Host " - $_" }
}
