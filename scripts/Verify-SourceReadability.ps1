#requires -Version 5.1
# Purpose: Audits first-party source readability with shared Purpose fragmentation rules and no source mutation.
[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ReportPath,
    [string]$InventoryPath,
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $invocationScriptPath = $MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($invocationScriptPath)) { throw 'Unable to resolve the script path. Supply -RepositoryRoot explicitly.' }
    $scriptDirectory = Split-Path -Parent $invocationScriptPath
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
}
$Repo=[System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92,47))
if (-not $ReportPath) { $ReportPath=Join-Path $Repo 'artifacts\source-readability\report.md' }
elseif (-not [System.IO.Path]::IsPathRooted($ReportPath)) { $ReportPath=Join-Path $Repo $ReportPath }
if (-not $InventoryPath) { $InventoryPath=Join-Path $Repo 'artifacts\source-readability\inventory.csv' }
elseif (-not [System.IO.Path]::IsPathRooted($InventoryPath)) { $InventoryPath=Join-Path $Repo $InventoryPath }
$ReportPath=[System.IO.Path]::GetFullPath($ReportPath)
$InventoryPath=[System.IO.Path]::GetFullPath($InventoryPath)
$Issues=New-Object 'System.Collections.Generic.List[object]'
$Inventory=New-Object 'System.Collections.Generic.List[object]'
. (Join-Path $PSScriptRoot 'SourcePurposeRules.ps1')

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

function Test-GenericPurpose([string]$Purpose) { return $GenericPurposes -contains $Purpose.Trim() }
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
function Add-Issue([string]$Severity,[string]$Code,[string]$Path,[string]$Message,[int]$Line=0) {
    $Issues.Add([pscustomobject]@{Severity=$Severity;Code=$Code;Path=$Path;Line=$Line;Message=$Message}) | Out-Null
}
function Get-PurposeMatches([string]$Text,[string]$Extension) {
    if ($Extension -eq '.razor') { return @([regex]::Matches($Text,'(?m)^\s*@\*\s*Purpose\s*:\s*(.+?)\s*\*@\s*$')) }
    return @([regex]::Matches($Text,'(?m)^\s*(?://|#)\s*Purpose\s*:\s*(.+?)\s*$'))
}
function Get-DeclaredTypeNames([string]$Text) {
    $pattern='(?m)^(?:public|internal)\s+(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record(?:\s+(?:class|struct))?|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)'
    return @([regex]::Matches($Text,$pattern) | ForEach-Object {$_.Groups[1].Value} | Select-Object -Unique)
}
function Get-BrokenPurposeCandidates([string]$FilePath,[string]$Text) {
    $candidates=New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    [void]$candidates.Add([System.IO.Path]::GetFileNameWithoutExtension($FilePath))
    foreach($typeName in @(Get-DeclaredTypeNames $Text)){[void]$candidates.Add($typeName)}
    foreach($part in @((Get-RelativePath $FilePath) -split '/')) {
        $name=[System.IO.Path]::GetFileNameWithoutExtension($part)
        if($name.Length -ge 3){[void]$candidates.Add($name)}
    }
    return @($candidates | ForEach-Object { [string]$_ })
}
function Test-BrokenPurpose([string]$Purpose,[string]$FilePath,[string]$Text) {
    return Test-BrokenPurposeText -Purpose $Purpose -Candidates @(Get-BrokenPurposeCandidates -FilePath $FilePath -Text $Text)
}
function Test-CompressedCodeLine([string]$Line) {
    if ($Line.Length -lt 180) { return $false }
    $semicolons=[regex]::Matches($Line,';').Count
    $joined=$Line -match '}\s*(?:else\s+)?(?:if|for|foreach|while|switch|try|catch|finally|public|private|internal|protected)\b'
    $controls=[regex]::Matches($Line,'\b(?:if|for|foreach|while|switch)\s*\(').Count -ge 2
    return $semicolons -ge 3 -or $joined -or $controls
}
function Get-LineNumberFromIndex([string]$Text,[int]$Index) {
    if($Index -le 0){return 1}
    return ([regex]::Matches($Text.Substring(0,$Index),"`n").Count + 1)
}

$extensions=@('.cs','.razor','.ps1','.py','.cpp','.c','.h','.hpp','.js','.ts','.tsx','.jsx')
foreach($rootName in @('src','tests','scripts','tools','services','collectors','plugins')) {
    $root=Join-Path $Repo $rootName
    if(-not(Test-Path -LiteralPath $root -PathType Container)){continue}
    foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        if(Test-ExcludedPath $file.FullName){continue}
        $extension=$file.Extension.ToLowerInvariant()
        if($extension -notin $extensions){continue}
        $text=[System.IO.File]::ReadAllText($file.FullName)
        if([string]::IsNullOrEmpty($text)){continue}
        $relative=Get-RelativePath $file.FullName
        $normalizedText=$text -replace "`r`n","`n" -replace "`r","`n"
        $lines=@($normalizedText -split "`n")
        $purposeMatches=@(Get-PurposeMatches $text $extension)
        $purpose=''
        if($purposeMatches.Count -eq 0){Add-Issue ERROR MISSING_PURPOSE $relative 'Handwritten first-party source has no Purpose comment.'}
        else {
            $purpose=$purposeMatches[0].Groups[1].Value.Trim()
            if($purposeMatches.Count -gt 1){Add-Issue ERROR DUPLICATE_PURPOSE $relative 'Exactly one Purpose comment is allowed.'}
            if($purpose.Length -lt 12 -or $purpose.Length -gt 180){Add-Issue WARN PURPOSE_LENGTH $relative "Purpose length is $($purpose.Length); expected 12-180 characters."}
            if(Test-BrokenPurpose $purpose $file.FullName $text){Add-Issue ERROR BROKEN_PURPOSE $relative 'Purpose contains a character-split filename, type, or path identifier.'}
            if(Test-GenericPurpose $purpose){Add-Issue ERROR GENERIC_PURPOSE $relative 'Purpose must describe the concrete responsibility of this source file.'}
        }
        for($i=0;$i -lt $lines.Count;$i++) {
            if(($extension -eq '.cs' -or $extension -eq '.razor') -and (Test-CompressedCodeLine $lines[$i])) { Add-Issue ERROR COMPRESSED_SOURCE $relative 'Line contains compressed code statements.' ($i+1) }
            if($extension -eq '.razor' -and $lines[$i].Length -ge 360 -and [regex]::Matches($lines[$i],'<[A-Za-z][^>]*>').Count -ge 8){Add-Issue WARN DENSE_RAZOR_MARKUP $relative 'Dense Razor markup should be split for reviewer readability.' ($i+1)}
        }
        if($extension -eq '.cs' -or $extension -eq '.razor') {
            foreach($match in [regex]::Matches($normalizedText,'(?s)\bcatch\s*(?:\([^)]*\))?\s*\{\s*\}')) {
                Add-Issue ERROR EMPTY_CATCH $relative 'Empty catch block hides failure state.' (Get-LineNumberFromIndex $normalizedText $match.Index)
            }
        }
        $Inventory.Add([pscustomobject]@{Path=$relative;Extension=$extension;Lines=$lines.Count;Purpose=$purpose})|Out-Null
    }
}

$reportDir=Split-Path -Parent $ReportPath; if($reportDir){New-Item -ItemType Directory -Force -Path $reportDir|Out-Null}
$inventoryDir=Split-Path -Parent $InventoryPath; if($inventoryDir){New-Item -ItemType Directory -Force -Path $inventoryDir|Out-Null}
$Inventory | Export-Csv -NoTypeInformation -Encoding utf8 $InventoryPath
$report=@('# Source Readability Report','',"Files scanned: $($Inventory.Count)","Findings: $($Issues.Count)",'')
foreach($issue in $Issues){$location=if($issue.Line -gt 0){":$($issue.Line)"}else{''};$report+="- [$($issue.Severity)] $($issue.Code) `$($issue.Path)$location`: $($issue.Message)"}
[System.IO.File]::WriteAllLines($ReportPath,$report,(New-Object System.Text.UTF8Encoding($false)))
if($Issues.Count -eq 0){Write-Host 'PASS: source readability audit found no issues.' -ForegroundColor Green; return}
$Issues | Sort-Object Severity,Path,Line | Format-Table -AutoSize | Out-Host
if($Strict){throw "Source readability audit failed with $($Issues.Count) finding(s)."}
