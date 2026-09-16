#requires -Version 5.1
# Purpose: Repairs malformed Purpose comments and formats only C# files that contain compressed statements.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$AllowDifferentRevision
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92, 47))
$ExpectedBaseSha = 'daeb0128b11a04317080fcd8623c9c9a8003ad6d'
$Solution = Join-Path $Repo 'DevelopmentIntelligencePlatform.sln'
$RepairScript = Join-Path $PSScriptRoot 'Repair-SourcePurposeComments.ps1'
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$AllowedPreExistingChanges = @(
    'scripts/Apply-SReadabilityFix.ps1',
    'scripts/Repair-SourcePurposeComments.ps1'
)

function Invoke-ExternalChecked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$FilePath failed with exit code $exitCode. Arguments: $($ArgumentList -join ' ')"
    }
}

function Get-RepoRelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    return ($full.Substring($Repo.Length) -replace '^[\\/]+', '').Replace([char]92, [char]47)
}

function Get-RelativeToDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $root = [System.IO.Path]::GetFullPath($Directory).TrimEnd([char[]]@(92, 47))
    $full = [System.IO.Path]::GetFullPath($Path)
    return ($full.Substring($root.Length) -replace '^[\\/]+', '').Replace([char]92, [char]47)
}

function Test-ExcludedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $rel = (Get-RepoRelativePath -Path $Path).Replace([char]47, [char]92)
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

    $candidateNormalized = ($Candidate -replace '[^A-Za-z0-9]', '')
    if ($candidateNormalized.Length -lt 4) {
        return $false
    }

    $tokens = @(
        [regex]::Matches($Purpose, '[A-Za-z0-9]+') |
        ForEach-Object { $_.Value }
    )

    for ($start = 0; $start -lt $tokens.Count; $start++) {
        $joined = ''
        $shortCount = 0

        for ($end = $start; $end -lt $tokens.Count -and $end -lt ($start + 32); $end++) {
            $token = $tokens[$end]
            $joined += $token

            if ($token.Length -le 2) {
                $shortCount++
            }

            $partCount = $end - $start + 1
            if ($joined.Length -gt $candidateNormalized.Length) {
                break
            }

            if (
                $partCount -ge 3 -and
                $shortCount -ge 2 -and
                $joined.Equals($candidateNormalized, [System.StringComparison]::OrdinalIgnoreCase)
            ) {
                return $true
            }
        }
    }

    return $false
}

function Test-BrokenPurposeText {
    param(
        [Parameter(Mandatory = $true)][string]$Purpose,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $candidates = New-Object 'System.Collections.Generic.List[string]'
    $candidates.Add([System.IO.Path]::GetFileNameWithoutExtension($FilePath)) | Out-Null

    foreach ($typeName in @(Get-DeclaredTypeNames -Content $Content)) {
        if (-not $candidates.Contains($typeName)) {
            $candidates.Add($typeName) | Out-Null
        }
    }

    foreach ($candidate in $candidates) {
        if (Test-FragmentedName -Purpose $Purpose -Candidate $candidate) {
            return $true
        }
    }

    return $false
}

function Get-BrokenPurposeFiles {
    $results = New-Object 'System.Collections.Generic.List[string]'
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

            if ($file.Extension.ToLowerInvariant() -notin $extensions) {
                continue
            }

            $content = [System.IO.File]::ReadAllText($file.FullName)
            if ([string]::IsNullOrEmpty($content) -or (Test-LicenseHeader -Text $content)) {
                continue
            }

            $matches = @([regex]::Matches($content, '(?m)^\s*(?://|#)\s*Purpose\s*:\s*(.+?)\s*$'))
            if ($matches.Count -eq 0) {
                continue
            }

            $purpose = $matches[0].Groups[1].Value.Trim()
            if (Test-BrokenPurposeText -Purpose $purpose -FilePath $file.FullName -Content $content) {
                $results.Add((Get-RepoRelativePath -Path $file.FullName)) | Out-Null
            }
        }
    }

    return @($results | Sort-Object -Unique)
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

        if (
            ($semicolonCount -ge 3) -or
            $hasStatementJoin -or
            $hasMultipleControlBlocks -or
            ($line.Length -ge 320 -and $hasMultipleAssignments)
        ) {
            $findings.Add([pscustomobject]@{
                Line = $i + 1
                Length = $line.Length
                Semicolons = $semicolonCount
            }) | Out-Null
        }
    }

    return @($findings)
}

function Get-CompressedCSharpFiles {
    $results = New-Object 'System.Collections.Generic.List[string]'

    foreach ($rootName in @('src', 'tests', 'services', 'collectors', 'plugins')) {
        $root = Join-Path $Repo $rootName
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            continue
        }

        foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs') {
            if (Test-ExcludedPath -Path $file.FullName) {
                continue
            }

            $text = [System.IO.File]::ReadAllText($file.FullName)
            if ([string]::IsNullOrEmpty($text) -or (Test-LicenseHeader -Text $text)) {
                continue
            }

            $normalized = $text -replace "`r`n", "`n" -replace "`r", "`n"
            $lines = @($normalized -split "`n")
            $findings = @(Get-CompressedCSharpLines -Lines $lines)
            if ($findings.Count -gt 0) {
                $results.Add((Get-RepoRelativePath -Path $file.FullName)) | Out-Null
            }
        }
    }

    return @($results | Sort-Object -Unique)
}

function Get-NearestProject {
    param([Parameter(Mandatory = $true)][string]$FilePath)

    $directory = [System.IO.DirectoryInfo](Split-Path -Parent $FilePath)
    $repoFull = [System.IO.Path]::GetFullPath($Repo)

    while ($null -ne $directory) {
        $dirFull = [System.IO.Path]::GetFullPath($directory.FullName)
        if (-not $dirFull.StartsWith($repoFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        $projects = @(Get-ChildItem -LiteralPath $directory.FullName -File -Filter '*.csproj' -ErrorAction SilentlyContinue)
        if ($projects.Count -eq 1) {
            return $projects[0].FullName
        }
        if ($projects.Count -gt 1) {
            throw "Multiple .csproj files found beside $FilePath. Specify formatting manually."
        }

        $directory = $directory.Parent
    }

    return $null
}

function Format-CompressedCSharpFiles {
    param([Parameter(Mandatory = $true)][string[]]$RelativePaths)

    if ($RelativePaths.Count -eq 0) {
        Write-Host 'No compressed C# files were detected.' -ForegroundColor Green
        return
    }

    $groups = @{}
    $unprojected = New-Object 'System.Collections.Generic.List[string]'

    foreach ($relative in $RelativePaths) {
        $full = Join-Path $Repo ($relative.Replace([char]47, [char]92))
        $project = Get-NearestProject -FilePath $full

        if ($null -eq $project) {
            $unprojected.Add($full) | Out-Null
            continue
        }

        if (-not $groups.ContainsKey($project)) {
            $groups[$project] = New-Object 'System.Collections.Generic.List[string]'
        }
        $groups[$project].Add($full) | Out-Null
    }

    foreach ($project in @($groups.Keys | Sort-Object)) {
        $projectDirectory = Split-Path -Parent $project
        $includes = @(
            $groups[$project] |
            ForEach-Object { Get-RelativeToDirectory -Directory $projectDirectory -Path $_ }
        )

        Write-Host "Formatting compressed C# files in: $(Get-RepoRelativePath -Path $project)" -ForegroundColor Cyan
        $arguments = @('format', $project, 'whitespace', '--include') + $includes
        Invoke-ExternalChecked -FilePath 'dotnet' -ArgumentList $arguments
    }

    if ($unprojected.Count -gt 0) {
        $tempRoot = Join-Path $Repo ('.dip-format-temp-' + [Guid]::NewGuid().ToString('N'))
        $tempProject = Join-Path $tempRoot 'Dip.ReadabilityFormatting.csproj'

        try {
            New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

            $compileLines = New-Object 'System.Collections.Generic.List[string]'
            $index = 0
            foreach ($full in $unprojected) {
                $index++
                $escaped = [System.Security.SecurityElement]::Escape([System.IO.Path]::GetFullPath($full))
                $compileLines.Add("    <Compile Include=`"$escaped`" Link=`"Source$index.cs`" />") | Out-Null
            }

            $projectXml = @(
                '<Project Sdk="Microsoft.NET.Sdk">'
                '  <PropertyGroup>'
                '    <TargetFramework>net8.0</TargetFramework>'
                '    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                '    <LangVersion>latest</LangVersion>'
                '  </PropertyGroup>'
                '  <ItemGroup>'
                ($compileLines -join "`n")
                '  </ItemGroup>'
                '</Project>'
                ''
            ) -join "`n"

            [System.IO.File]::WriteAllText($tempProject, $projectXml, $Utf8NoBom)

            Write-Host 'Formatting compressed C# files that are not attached to a committed project.' -ForegroundColor Cyan
            Invoke-ExternalChecked -FilePath 'dotnet' -ArgumentList @('format', $tempProject, 'whitespace')
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

function Get-TrackedDiffPaths {
    $raw = @(& git -C $Repo diff --name-only --diff-filter=ACMRT)
    if ($LASTEXITCODE -ne 0) {
        throw 'git diff --name-only failed.'
    }

    return @(
        $raw |
        ForEach-Object { $_.Trim().Replace([char]92, [char]47) } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
    )
}

function Restore-NewTrackedChanges {
    $paths = @(Get-TrackedDiffPaths)
    foreach ($path in $paths) {
        if ($path -in $AllowedPreExistingChanges) {
            continue
        }

        & git -C $Repo restore --worktree -- $path
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not restore: $path"
        }
    }
}

function New-ChangedFilesOverlay {
    param([Parameter(Mandatory = $true)][string[]]$Paths)

    if ($Paths.Count -eq 0) {
        throw 'No changed tracked files were found; overlay ZIP was not created.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $outputDirectory = Join-Path $Repo 'artifacts\source-readability'
    $staging = Join-Path ([System.IO.Path]::GetTempPath()) ('dip-readability-overlay-' + [Guid]::NewGuid().ToString('N'))
    $zipPath = Join-Path $outputDirectory 'DIP_S_Readability_Fix_Overlay_daeb0128.zip'

    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    try {
        foreach ($relative in $Paths) {
            $source = Join-Path $Repo ($relative.Replace([char]47, [char]92))
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                throw "Changed file was not found while building overlay: $relative"
            }

            $destination = Join-Path $staging ($relative.Replace([char]47, [char]92))
            $parent = Split-Path -Parent $destination
            if (-not (Test-Path -LiteralPath $parent)) {
                New-Item -ItemType Directory -Path $parent -Force | Out-Null
            }

            Copy-Item -LiteralPath $source -Destination $destination -Force
        }

        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $staging,
            $zipPath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false
        )

        $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
        return [pscustomobject]@{
            Path = $zipPath
            Sha256 = $hash
        }
    }
    finally {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

if (-not (Test-Path -LiteralPath $Solution -PathType Leaf)) {
    throw "DevelopmentIntelligencePlatform.sln was not found under: $Repo"
}
if (-not (Test-Path -LiteralPath $RepairScript -PathType Leaf)) {
    throw "Repair script was not found: $RepairScript"
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git was not found.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet was not found. Install/use the repository .NET 8 SDK before applying this package.'
}

$headOutput = @(& git -C $Repo rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $headOutput.Count -eq 0) {
    throw 'Could not resolve repository HEAD.'
}
$head = $headOutput[0].Trim()
if (-not $AllowDifferentRevision -and $head -ne $ExpectedBaseSha) {
    throw "Base revision mismatch. Expected $ExpectedBaseSha but found $head."
}

$statusLines = @(& git -C $Repo status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw 'git status failed.'
}

$unexpectedChanges = New-Object 'System.Collections.Generic.List[string]'
foreach ($line in $statusLines) {
    if ($line.Length -lt 4) {
        continue
    }

    $path = $line.Substring(3).Trim().Replace([char]92, [char]47)
    if ($path -match ' -> ') {
        $path = ($path -split ' -> ')[-1].Trim()
    }

    if ($path -notin $AllowedPreExistingChanges -and -not $path.StartsWith('artifacts/source-readability/')) {
        $unexpectedChanges.Add($line) | Out-Null
    }
}

if ($unexpectedChanges.Count -gt 0) {
    Write-Host 'Unexpected pre-existing working-tree changes:' -ForegroundColor Red
    $unexpectedChanges | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    throw 'Start from a clean daeb0128 worktree, then overlay only the two scripts from this package.'
}

$brokenBefore = @(Get-BrokenPurposeFiles)
$compressedBefore = @(Get-CompressedCSharpFiles)

Write-Host ''
Write-Host 'Detected scope' -ForegroundColor Cyan
Write-Host "  Broken Purpose comments : $($brokenBefore.Count)"
Write-Host "  Compressed C# files     : $($compressedBefore.Count)"
Write-Host ''

try {
    Write-Host 'Stage 1/4 - Repair malformed Purpose comments only' -ForegroundColor Cyan
    & $RepairScript -RepositoryRoot $Repo -BrokenOnly
    if ($LASTEXITCODE -ne 0) {
        throw 'Purpose repair failed.'
    }

    Write-Host ''
    Write-Host 'Stage 2/4 - Format only C# files detected as compressed' -ForegroundColor Cyan
    Format-CompressedCSharpFiles -RelativePaths $compressedBefore

    Write-Host ''
    Write-Host 'Stage 3/4 - Re-scan the two requested issue classes' -ForegroundColor Cyan
    $brokenAfter = @(Get-BrokenPurposeFiles)
    $compressedAfter = @(Get-CompressedCSharpFiles)

    if ($brokenAfter.Count -gt 0) {
        Write-Host 'Malformed Purpose comments remain:' -ForegroundColor Red
        $brokenAfter | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        throw "Malformed Purpose verification failed with $($brokenAfter.Count) file(s)."
    }

    if ($compressedAfter.Count -gt 0) {
        Write-Host 'Compressed C# remains:' -ForegroundColor Red
        $compressedAfter | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        throw "Compressed C# verification failed with $($compressedAfter.Count) file(s)."
    }

    & git -C $Repo diff --check
    if ($LASTEXITCODE -ne 0) {
        throw 'git diff --check failed.'
    }

    $allowedChangeSet = @($AllowedPreExistingChanges + $brokenBefore + $compressedBefore | Sort-Object -Unique)
    $changedPaths = @(Get-TrackedDiffPaths)
    $outsideScope = @($changedPaths | Where-Object { $_ -notin $allowedChangeSet })

    if ($outsideScope.Count -gt 0) {
        Write-Host 'Unexpected changed files outside the requested scope:' -ForegroundColor Red
        $outsideScope | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        throw 'The formatter changed files outside the requested two issue classes.'
    }

    Write-Host ''
    Write-Host 'Stage 4/4 - Create changed-files-only overlay ZIP' -ForegroundColor Cyan
    $overlay = New-ChangedFilesOverlay -Paths $changedPaths

    Write-Host ''
    Write-Host 'S-only readability cleanup completed successfully.' -ForegroundColor Green
    Write-Host "Broken Purpose remaining : $($brokenAfter.Count)" -ForegroundColor Green
    Write-Host "Compressed C# remaining  : $($compressedAfter.Count)" -ForegroundColor Green
    Write-Host "Changed tracked files    : $($changedPaths.Count)" -ForegroundColor Green
    Write-Host "Overlay ZIP              : $($overlay.Path)" -ForegroundColor Green
    Write-Host "Overlay SHA-256          : $($overlay.Sha256)" -ForegroundColor Green
    Write-Host ''
    Write-Host 'The working tree is left with the verified source changes ready for review and commit.' -ForegroundColor Yellow
}
catch {
    Write-Host ''
    Write-Host 'Cleanup failed. Restoring newly modified tracked source files.' -ForegroundColor Red
    Restore-NewTrackedChanges
    throw
}
