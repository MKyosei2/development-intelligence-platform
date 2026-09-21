#requires -Version 5.1
# Purpose: Audits first-party source readability, Purpose coverage, empty catch blocks, and compressed C#/Razor code.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$ReportPath,
    [string]$InventoryPath,
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$Repo=[System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92,47))
if (-not $ReportPath) { $ReportPath=Join-Path $Repo 'artifacts\source-readability\report.md' }
if (-not $InventoryPath) { $InventoryPath=Join-Path $Repo 'artifacts\source-readability\inventory.csv' }
$Issues=New-Object 'System.Collections.Generic.List[object]'
$Inventory=New-Object 'System.Collections.Generic.List[object]'

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
function Test-FragmentedPurpose([string]$Purpose,[string]$Stem) {
    $normalized=$Stem -replace '[^A-Za-z0-9]',''
    if ($normalized.Length -lt 4) { return $false }
    $tokens=@([regex]::Matches($Purpose,'[A-Za-z0-9]+') | ForEach-Object {$_.Value})
    for($start=0;$start -lt $tokens.Count;$start++) {
        $joined='';$short=0
        for($end=$start;$end -lt $tokens.Count -and $end -lt ($start+32);$end++) {
            $joined+=$tokens[$end]; if($tokens[$end].Length -le 2){$short++}
            if($joined.Length -gt $normalized.Length){break}
            if(($end-$start+1)-ge 3 -and $short -ge 2 -and $joined.Equals($normalized,[System.StringComparison]::OrdinalIgnoreCase)){return $true}
        }
    }
    return $false
}
function Test-CompressedCodeLine([string]$Line) {
    if ($Line.Length -lt 180) { return $false }
    $semicolons=[regex]::Matches($Line,';').Count
    $joined=$Line -match '}\s*(?:else\s+)?(?:if|for|foreach|while|switch|try|catch|finally|public|private|internal|protected)\b'
    $controls=[regex]::Matches($Line,'\b(?:if|for|foreach|while|switch)\s*\(').Count -ge 2
    return $semicolons -ge 3 -or $joined -or $controls
}

$extensions=@('.cs','.razor','.ps1','.py','.cpp','.c','.h','.hpp','.js','.ts','.tsx','.jsx')
foreach($rootName in @('src','tests','scripts','tools','services','collectors','plugins')) {
    $root=Join-Path $Repo $rootName
    if(-not(Test-Path -LiteralPath $root -PathType Container)){continue}
    foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        if(Test-ExcludedPath $file.FullName){continue}
        $extension=$file.Extension.ToLowerInvariant(); if($extension -notin $extensions){continue}
        $text=[System.IO.File]::ReadAllText($file.FullName); if([string]::IsNullOrEmpty($text)){continue}
        $relative=Get-RelativePath $file.FullName
        $lines=@(($text -replace "`r`n","`n" -replace "`r","`n") -split "`n")
        $purposeMatches=Get-PurposeMatches $text $extension
        $purpose=''
        if($purposeMatches.Count -eq 0){Add-Issue ERROR MISSING_PURPOSE $relative 'Handwritten first-party source has no Purpose comment.'}
        else {
            $purpose=$purposeMatches[0].Groups[1].Value.Trim()
            if($purposeMatches.Count -gt 1){Add-Issue ERROR DUPLICATE_PURPOSE $relative 'Exactly one Purpose comment is allowed.'}
            if($purpose.Length -lt 12 -or $purpose.Length -gt 180){Add-Issue WARN PURPOSE_LENGTH $relative "Purpose length is $($purpose.Length); expected 12-180 characters."}
            if(Test-FragmentedPurpose $purpose ([System.IO.Path]::GetFileNameWithoutExtension($file.FullName))){Add-Issue ERROR BROKEN_PURPOSE $relative 'Purpose contains character-split filename/type text.'}
        }
        for($i=0;$i -lt $lines.Count;$i++) {
            if(($extension -eq '.cs' -or $extension -eq '.razor') -and (Test-CompressedCodeLine $lines[$i])) { Add-Issue ERROR COMPRESSED_SOURCE $relative 'Line contains compressed code statements.' ($i+1) }
            if(($extension -eq '.cs' -or $extension -eq '.razor') -and $lines[$i] -match '\bcatch\s*\{\s*\}') { Add-Issue ERROR EMPTY_CATCH $relative 'Empty catch block hides failure state.' ($i+1) }
            if($extension -eq '.razor' -and $lines[$i].Length -ge 360 -and [regex]::Matches($lines[$i],'<[A-Za-z][^>]*>').Count -ge 8){Add-Issue WARN DENSE_RAZOR_MARKUP $relative 'Dense Razor markup should be split for reviewer readability.' ($i+1)}
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
