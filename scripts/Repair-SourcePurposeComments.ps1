#requires -Version 5.1
# Purpose: Repairs missing, duplicate, malformed, and unreadable Purpose comments in first-party handwritten source, including Razor and tooling source.
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RepositoryRoot,
    [switch]$WhatIfReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $invocationScriptPath = $MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($invocationScriptPath)) {
        throw 'Unable to resolve the script path. Supply -RepositoryRoot explicitly.'
    }
    $scriptDirectory = Split-Path -Parent $invocationScriptPath
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
}
$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92, 47))
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$Changed = New-Object 'System.Collections.Generic.List[string]'

$GenericPurposes = @(
    'Verifies expected behavior and invariants for the surrounding feature.',
    'Automates a repeatable development or verification workflow for this repository.',
    'Provides repository tooling used by development and verification workflows.',
    'Renders and coordinates this web surface and its user interactions.',
    'Exposes HTTP endpoints for this API responsibility.',
    'Defines a persistence boundary used by the application layer.',
    'Persists and queries data for this application boundary.',
    'Runs background processing for this application responsibility.',
    'Coordinates application behavior for this feature area.',
    'Defines data exchanged across this application boundary.',
    'Defines domain behavior and state for this feature area.',
    'Provides infrastructure behavior for this application boundary.',
    'Implements web-facing behavior for this application surface.',
    'Defines a focused responsibility within the development intelligence platform.'
)

function Test-GenericPurpose {
    param([Parameter(Mandatory = $true)][string]$Purpose)
    return $GenericPurposes -contains $Purpose.Trim()
}

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    return ($full.Substring($Repo.Length) -replace '^[\\/]+', '').Replace([char]92, [char]47)
}

function Test-ExcludedPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $rel = (Get-RelativePath $Path).Replace([char]47, [char]92)
    if ($rel -match '(^|\\)(\.git|bin|obj|node_modules|packages|vendor|third_party|ThirdParty|Intermediate|Binaries|Library|Temp|Logs|Migrations|Generated|generated)(\\|$)') { return $true }
    $name = [System.IO.Path]::GetFileName($Path)
    return $name -match '\.(g|generated|designer)\.cs$' -or $name -in @('AssemblyInfo.cs', 'GlobalUsings.g.cs')
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
    return @([regex]::Matches($Content, $pattern) | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
}

function Get-PurposeMatch {
    param([string]$Line, [string]$Extension)
    if ($Extension -eq '.razor') { return [regex]::Match($Line, '^\s*@\*\s*Purpose\s*:\s*(.*?)\s*\*@\s*$') }
    return [regex]::Match($Line, '^\s*(?://|#)\s*Purpose\s*:\s*(.*?)\s*$')
}

function Test-FragmentedName {
    param([string]$Purpose,[string]$Candidate)
    $candidateNormalized = $Candidate -replace '[^A-Za-z0-9]', ''
    if ($candidateNormalized.Length -lt 4) { return $false }
    $tokens = @([regex]::Matches($Purpose, '[A-Za-z0-9]+') | ForEach-Object { $_.Value })
    for ($start = 0; $start -lt $tokens.Count; $start++) {
        $joined=''; $shortCount=0
        for ($end=$start; $end -lt $tokens.Count -and $end -lt ($start+32); $end++) {
            $joined += $tokens[$end]
            if ($tokens[$end].Length -le 2) { $shortCount++ }
            if ($joined.Length -gt $candidateNormalized.Length) { break }
            if (($end-$start+1) -ge 3 -and $shortCount -ge 2 -and $joined.Equals($candidateNormalized,[System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        }
    }
    return $false
}

function Test-BrokenPurpose {
    param([string]$Purpose,[string]$FilePath,[string]$Content)
    $candidates = New-Object 'System.Collections.Generic.List[string]'
    $candidates.Add([System.IO.Path]::GetFileNameWithoutExtension($FilePath)) | Out-Null
    foreach ($typeName in @(Get-DeclaredTypeNames -Content $Content)) { if (-not $candidates.Contains($typeName)) { $candidates.Add($typeName) | Out-Null } }
    foreach ($candidate in $candidates) { if (Test-FragmentedName -Purpose $Purpose -Candidate $candidate) { return $true } }
    return $false
}

function Get-FeatureLabel {
    param([Parameter(Mandatory = $true)][string]$FilePath)
    $relative = Get-RelativePath $FilePath
    $parts = @($relative -split '/')
    $ignored = @(
        'src','tests','scripts','tools','services','collectors','plugins',
        'Features','Feature','Entities','Entity','Enums','Enum','ValueObjects',
        'Contracts','Dtos','DTOs','Commands','Queries','Services','Repositories',
        'Configurations','Models','Abstractions','Middleware','Extensions',
        'Workers','Support','Common'
    )

    for ($i = $parts.Count - 2; $i -ge 0; $i--) {
        $candidate = $parts[$i]
        if ([string]::IsNullOrWhiteSpace($candidate) -or $candidate -in $ignored) { continue }
        if ($candidate -match '^Dip\.') { continue }
        return (ConvertTo-HumanName $candidate).ToLowerInvariant()
    }

    return 'this repository area'
}

function Get-SubjectLabel {
    param([Parameter(Mandatory = $true)][string]$Name)
    $subject = $Name -replace '(Tests?|Repository|Controller|Worker|Job|Service|Dto|DTO|Request|Response|Contract|Options|Configuration|Extensions)$',''
    if ([string]::IsNullOrWhiteSpace($subject)) { $subject = $Name }
    return ConvertTo-HumanName $subject
}

function Get-ReplacementPurpose {
    param([string]$FilePath,[string]$Content)
    $relative = Get-RelativePath $FilePath
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    $names = @(Get-DeclaredTypeNames -Content $Content)
    $name = if ($names.Count -eq 1) { $names[0] } else { $baseName }
    $subject = Get-SubjectLabel $name
    $feature = Get-FeatureLabel $FilePath

    if ($baseName -eq 'Program') {
        return "Composes and starts the $feature host with its configured dependencies."
    }
    if ($relative -match '^tests/') {
        return "Verifies $subject behavior, edge cases, and invariants for $feature."
    }
    if ($relative -match '^scripts/') {
        return "Automates $subject for repository development and verification workflows."
    }
    if ($relative -match '^tools/') {
        return "Implements $subject tooling used by repository verification and development workflows."
    }
    if ($relative -match '^src/Dip\.Web/.+\.razor$') {
        return "Renders the $subject UI and coordinates its $feature interactions."
    }
    if ($name -match 'Controller$') {
        return "Exposes $subject HTTP endpoints for $feature."
    }
    if ($name -match '^I[A-Z].*Repository$') {
        return "Defines persistence operations required by $subject in $feature."
    }
    if ($name -match 'Repository$') {
        return "Persists and queries $subject state for $feature."
    }
    if ($name -match 'Worker$|Job$') {
        return "Runs $subject background processing for $feature."
    }
    if ($name -match 'Service$') {
        return "Coordinates $subject application operations for $feature."
    }
    if ($name -match 'Dto$|DTO$|Request$|Response$|Contract') {
        return "Defines $subject data exchanged by $feature."
    }
    if ($relative -match '^src/Dip\.Domain/') {
        return "Defines $subject domain behavior and invariants for $feature."
    }
    if ($relative -match '^src/Dip\.Infrastructure/') {
        return "Implements $subject infrastructure integration for $feature."
    }
    if ($relative -match '^src/Dip\.Web/') {
        return "Implements $subject web behavior for $feature."
    }
    return "Implements $subject responsibilities for $feature."
}

function Get-PurposeLine {
    param([string]$Extension,[string]$Purpose)
    if ($Extension -eq '.razor') { return "@* Purpose: $Purpose *@" }
    if ($Extension -in @('.ps1','.py')) { return "# Purpose: $Purpose" }
    return "// Purpose: $Purpose"
}

function Get-InsertionIndex {
    param([System.Collections.Generic.List[string]]$Lines,[string]$Extension)
    $index=0
    if ($Extension -eq '.ps1') { while ($index -lt $Lines.Count -and $Lines[$index] -match '^\s*#requires\b') { $index++ } }
    elseif ($Extension -eq '.py') {
        if ($index -lt $Lines.Count -and $Lines[$index] -match '^#!') { $index++ }
        if ($index -lt $Lines.Count -and $Lines[$index] -match '^#.*coding[:=]') { $index++ }
    }
    elseif ($Extension -eq '.razor') { return 0 }
    return $index
}

function Assert-PurposeIntegrity {
    $failures=New-Object 'System.Collections.Generic.List[string]'
    $extensions = @('.cs','.razor','.ps1','.py','.cpp','.c','.h','.hpp','.js','.ts','.tsx','.jsx')
    foreach ($rootName in @('src','tests','scripts','tools','services','collectors','plugins')) {
        $root = Join-Path $Repo $rootName
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
            if (Test-ExcludedPath $file.FullName) { continue }
            $extension=$file.Extension.ToLowerInvariant()
            if ($extension -notin $extensions) { continue }
            $content=[System.IO.File]::ReadAllText($file.FullName)
            if ([string]::IsNullOrEmpty($content)) { continue }
            $matches=New-Object 'System.Collections.Generic.List[object]'
            foreach($line in (($content -replace "`r`n","`n" -replace "`r","`n") -split "`n")) {
                $match=Get-PurposeMatch -Line $line -Extension $extension
                if($match.Success){$matches.Add($match) | Out-Null}
            }
            $relative=Get-RelativePath $file.FullName
            if($matches.Count -ne 1){$failures.Add("${relative}: expected exactly one Purpose comment; found $($matches.Count)") | Out-Null; continue}
            $purpose=$matches[0].Groups[1].Value.Trim()
            if(Test-BrokenPurpose -Purpose $purpose -FilePath $file.FullName -Content $content){$failures.Add("${relative}: Purpose comment is still fragmented") | Out-Null}
            if(Test-GenericPurpose -Purpose $purpose){$failures.Add("${relative}: Purpose comment is generic and must describe the concrete responsibility") | Out-Null}
        }
    }
    if($failures.Count -gt 0){throw "Purpose integrity verification failed:`n$($failures -join "`n")"}
    Write-Host 'PASS: Purpose comments are present, unique, and non-fragmented.' -ForegroundColor Green
}

$extensions = @('.cs','.razor','.ps1','.py','.cpp','.c','.h','.hpp','.js','.ts','.tsx','.jsx')
foreach ($rootName in @('src','tests','scripts','tools','services','collectors','plugins')) {
    $root = Join-Path $Repo $rootName
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        if (Test-ExcludedPath $file.FullName) { continue }
        $extension=$file.Extension.ToLowerInvariant()
        if ($extension -notin $extensions) { continue }
        $content=[System.IO.File]::ReadAllText($file.FullName)
        if ([string]::IsNullOrEmpty($content)) { continue }
        $normalized=$content -replace "`r`n","`n" -replace "`r","`n"
        $lines=New-Object 'System.Collections.Generic.List[string]'
        foreach ($line in ($normalized -split "`n")) { $lines.Add($line) | Out-Null }
        $indexes=New-Object 'System.Collections.Generic.List[int]'
        for ($i=0;$i -lt [Math]::Min(80,$lines.Count);$i++) {
            $match=Get-PurposeMatch -Line $lines[$i] -Extension $extension
            if ($match.Success) { $indexes.Add($i) | Out-Null }
        }
        $replacement=Get-ReplacementPurpose -FilePath $file.FullName -Content $content
        $replacementLine=Get-PurposeLine -Extension $extension -Purpose $replacement
        $needsChange=$false
        if ($indexes.Count -eq 0) {
            $lines.Insert((Get-InsertionIndex -Lines $lines -Extension $extension),$replacementLine)
            $needsChange=$true
        } else {
            $first=$indexes[0]
            $purpose=(Get-PurposeMatch -Line $lines[$first] -Extension $extension).Groups[1].Value.Trim()
            if ($purpose.Length -lt 12 -or $purpose.Length -gt 180 -or (Test-BrokenPurpose -Purpose $purpose -FilePath $file.FullName -Content $content) -or (Test-GenericPurpose -Purpose $purpose)) {
                $lines[$first]=$replacementLine; $needsChange=$true
            }
            if ($indexes.Count -gt 1) {
                for ($j=$indexes.Count-1;$j -ge 1;$j--) { $lines.RemoveAt($indexes[$j]) }
                $needsChange=$true
            }
        }
        if (-not $needsChange) { continue }
        $relative=Get-RelativePath $file.FullName
        if ($WhatIfReport -or $WhatIfPreference) { Write-Host "[WOULD FIX] $relative" -ForegroundColor Yellow; continue }
        if ($PSCmdlet.ShouldProcess($relative,'Normalize Purpose comment')) {
            [System.IO.File]::WriteAllText($file.FullName,(($lines -join "`n").TrimEnd("`n")+"`n"),$Utf8NoBom)
            $Changed.Add($relative) | Out-Null
            Write-Host "[FIXED] $relative" -ForegroundColor Green
        }
    }
}
Write-Host "Purpose normalization complete. Changed: $($Changed.Count)" -ForegroundColor Cyan
if(-not $WhatIfReport -and -not $WhatIfPreference){Assert-PurposeIntegrity}
