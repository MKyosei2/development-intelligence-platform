#requires -Version 5.1
# Purpose: Reports residual C# and Razor readability findings without rewriting source files.
[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ReportPath,
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $invocationScriptPath = $MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($invocationScriptPath)) { throw 'Unable to resolve the script path. Supply -RepositoryRoot explicitly.' }
    $scriptDirectory = Split-Path -Parent $invocationScriptPath
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
}
$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92,47))
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $Repo 'artifacts\source-readability\residuals.md'
}
$Findings = New-Object 'System.Collections.Generic.List[object]'

function Get-RelativePath([string]$Path) {
    $full=[System.IO.Path]::GetFullPath($Path)
    return ($full.Substring($Repo.Length) -replace '^[\\/]+','').Replace([char]92,[char]47)
}
function Test-ExcludedPath([string]$Path) {
    $rel=(Get-RelativePath $Path).Replace([char]47,[char]92)
    if ($rel -match '(^|\\)(\.git|bin|obj|node_modules|packages|vendor|third_party|ThirdParty|Intermediate|Binaries|Library|Temp|Logs|Migrations|Generated|generated)(\\|$)') { return $true }
    $name=[System.IO.Path]::GetFileName($Path)
    return $name -match '\.(g|generated|designer)\.cs$' -or $name -in @('AssemblyInfo.cs','GlobalUsings.g.cs')
}
function Test-CompressedCodeLine([string]$Line) {
    if ($Line.Length -lt 180) { return $false }
    $semicolons=[regex]::Matches($Line,';').Count
    $joined=$Line -match '}\s*(?:else\s+)?(?:if|for|foreach|while|switch|try|catch|finally|public|private|internal|protected)\b'
    $controls=[regex]::Matches($Line,'\b(?:if|for|foreach|while|switch)\s*\(').Count -ge 2
    return $semicolons -ge 3 -or $joined -or $controls
}
function Add-Finding([string]$Code,[string]$Path,[int]$Line,[string]$Message) {
    $Findings.Add([pscustomobject]@{Code=$Code;Path=$Path;Line=$Line;Message=$Message}) | Out-Null
}

foreach($rootName in @('src','tests','collectors','plugins')) {
    $root=Join-Path $Repo $rootName
    if(-not(Test-Path -LiteralPath $root -PathType Container)){continue}
    foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        if(Test-ExcludedPath $file.FullName){continue}
        $extension=$file.Extension.ToLowerInvariant()
        if($extension -notin @('.cs','.razor')){continue}
        $content=[System.IO.File]::ReadAllText($file.FullName)
        if([string]::IsNullOrEmpty($content)){continue}
        $normalized=$content -replace "`r`n","`n" -replace "`r","`n"
        $lines=@($normalized -split "`n")
        $relative=Get-RelativePath $file.FullName
        for($i=0;$i -lt $lines.Count;$i++) {
            if(Test-CompressedCodeLine $lines[$i]) { Add-Finding 'COMPRESSED_SOURCE' $relative ($i+1) 'Use dotnet format or a targeted manual refactor; this reporter never rewrites code.' }
            if($extension -eq '.razor' -and $lines[$i].Length -ge 360 -and [regex]::Matches($lines[$i],'<[A-Za-z][^>]*>').Count -ge 8) {
                Add-Finding 'DENSE_RAZOR_MARKUP' $relative ($i+1) 'Split dense markup manually or into a focused component.'
            }
        }
        foreach($match in [regex]::Matches($normalized,'(?s)\bcatch\s*(?:\([^)]*\))?\s*\{\s*\}')) {
            $line=([regex]::Matches($normalized.Substring(0,$match.Index),"`n").Count+1)
            Add-Finding 'EMPTY_CATCH' $relative $line 'Document or handle the exception intentionally; this reporter does not inject comments.'
        }
    }
}

$directory=Split-Path -Parent $ReportPath
if($directory){New-Item -ItemType Directory -Force -Path $directory|Out-Null}
$report=@(
    '# Residual Source Readability Report','',
    'This command is report-only. It never changes C# or Razor source.','',
    "Findings: $($Findings.Count)",''
)
foreach($finding in $Findings){$report+="- $($finding.Code) `$($finding.Path):$($finding.Line)`: $($finding.Message)"}
[System.IO.File]::WriteAllLines($ReportPath,$report,(New-Object System.Text.UTF8Encoding($false)))
if($Findings.Count -eq 0){Write-Host 'PASS: no residual C#/Razor readability findings.' -ForegroundColor Green; return}
$Findings | Format-Table -AutoSize | Out-Host
Write-Warning "Residual readability findings were reported only; no source file was modified. Report: $ReportPath"
if($Strict){throw "Residual source readability report contains $($Findings.Count) finding(s)."}
