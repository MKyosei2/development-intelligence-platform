#requires -Version 5.1
# Purpose: Repairs only existing Purpose comments whose identifiers were incorrectly fragmented into character groups.
[CmdletBinding()]
param([string]$RepositoryRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92,47))
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot 'SourcePurposeRules.ps1')

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
function ConvertTo-HumanName([string]$Name) {
    $value=$Name -replace '([a-z0-9])([A-Z])','$1 $2'
    $value=$value -replace '([A-Z]+)([A-Z][a-z])','$1 $2'
    $value=(($value -replace '[_\-]+',' ') -replace '\s+',' ').Trim()
    $value=$value -replace '\bQa\b','QA'
    $value=$value -replace '\bUi\b','UI'
    $value=$value -replace '\bApi\b','API'
    $value=$value -replace '\bCi\b','CI'
    $value=$value -replace '\bGui\b','GUI'
    $value=$value -replace '\bGit Hub\b','GitHub'
    $value=$value -replace '\bC Sharp\b','C#'
    $value=$value -replace '\bCpp\b','C++'
    $value=$value -replace '\bIde\b','IDE'
    return $value
}
function Get-DeclaredTypeNames([string]$Content) {
    $pattern='(?m)^(?:public|internal)\s+(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record(?:\s+(?:class|struct))?|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)'
    return @([regex]::Matches($Content,$pattern) | ForEach-Object {$_.Groups[1].Value} | Select-Object -Unique)
}
function Get-Candidates([string]$FilePath,[string]$Content) {
    $set=New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    [void]$set.Add([System.IO.Path]::GetFileNameWithoutExtension($FilePath))
    foreach($n in @(Get-DeclaredTypeNames $Content)){[void]$set.Add($n)}
    foreach($part in @((Get-RelativePath $FilePath) -split '/')){
        $n=[System.IO.Path]::GetFileNameWithoutExtension($part)
        if($n.Length -ge 3){[void]$set.Add($n)}
    }
    return @($set)
}
function Get-Feature([string]$FilePath) {
    $parts=@((Get-RelativePath $FilePath) -split '/')
    $ignored=@('src','tests','scripts','tools','services','collectors','plugins','Features','Feature','Entities','Entity','Enums','Enum','ValueObjects','Contracts','Dtos','DTOs','Commands','Queries','Services','Repositories','Configurations','Models','Abstractions','Middleware','Extensions','Workers','Support','Common')
    for($i=$parts.Count-2;$i -ge 0;$i--){
        $c=$parts[$i]
        if([string]::IsNullOrWhiteSpace($c) -or $c -in $ignored -or $c -match '^Dip\.') { continue }
        return (ConvertTo-HumanName $c).ToLowerInvariant()
    }
    return 'this repository area'
}
function Get-Subject([string]$Name) {
    $s=$Name -replace '(Tests?|Repository|Controller|Worker|Job|Service|Dto|DTO|Request|Response|Contract|Options|Configuration|Extensions)$',''
    if([string]::IsNullOrWhiteSpace($s)){$s=$Name}
    return ConvertTo-HumanName $s
}
function Get-Replacement([string]$FilePath,[string]$Content) {
    $relative=Get-RelativePath $FilePath
    $base=[System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    $names=@(Get-DeclaredTypeNames $Content)
    $name=if($names.Count -eq 1){$names[0]}else{$base}
    $subject=Get-Subject $name
    $feature=Get-Feature $FilePath
    if($relative -match '^tests/') { return "Verifies $subject behavior, edge cases, and invariants for $feature." }
    if($relative -match '^scripts/') { return "Automates $subject for repository development and verification workflows." }
    if($relative -match '^tools/') { return "Provides $subject tooling for repository verification and development workflows." }
    if($relative -match '^src/Dip\.Web/.+\.razor$') { return "Renders the $subject UI and coordinates its $feature interactions." }
    if($name -match 'Controller$') { return "Exposes $subject HTTP endpoints for $feature." }
    if($name -match 'Repository$') { return "Persists and queries $subject state for $feature." }
    if($name -match 'Worker$|Job$') { return "Runs $subject background processing for $feature." }
    if($name -match 'Service$') { return "Coordinates $subject application operations for $feature." }
    if($name -match 'Dto$|DTO$|Request$|Response$|Contract') { return "Defines $subject data exchanged by $feature." }
    if($relative -match '^src/Dip\.Domain/') { return "Defines $subject domain behavior and invariants for $feature." }
    if($relative -match '^src/Dip\.Infrastructure/') { return "Implements $subject infrastructure behavior for $feature." }
    if($relative -match '^src/Dip\.Web/') { return "Implements $subject web behavior for $feature." }
    return "Implements $subject responsibilities for $feature."
}
function Get-PurposeMatch([string]$Line,[string]$Ext) {
    if($Ext -eq '.razor'){return [regex]::Match($Line,'^\s*@\*\s*Purpose\s*:\s*(.*?)\s*\*@\s*$')}
    return [regex]::Match($Line,'^\s*(?://|#)\s*Purpose\s*:\s*(.*?)\s*$')
}
function Get-PurposeLine([string]$Ext,[string]$Purpose) {
    if($Ext -eq '.razor'){return "@* Purpose: $Purpose *@"}
    if($Ext -in @('.ps1','.py')){return "# Purpose: $Purpose"}
    return "// Purpose: $Purpose"
}

$extensions=@('.cs','.razor','.ps1','.py','.cpp','.c','.h','.hpp','.js','.ts','.tsx','.jsx')
$changed=New-Object 'System.Collections.Generic.List[string]'
foreach($rootName in @('src','tests','scripts','tools','services','collectors','plugins')){
    $root=Join-Path $Repo $rootName
    if(-not(Test-Path -LiteralPath $root -PathType Container)){continue}
    foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File){
        if(Test-ExcludedPath $file.FullName){continue}
        $ext=$file.Extension.ToLowerInvariant()
        if($ext -notin $extensions){continue}
        $content=[System.IO.File]::ReadAllText($file.FullName)
        if([string]::IsNullOrEmpty($content)){continue}
        $normalized=$content -replace "`r`n","`n" -replace "`r","`n"
        $lines=New-Object 'System.Collections.Generic.List[string]'
        foreach($line in ($normalized -split "`n")){$lines.Add($line)|Out-Null}
        $idx=-1; $purpose=''
        for($i=0;$i -lt [Math]::Min(80,$lines.Count);$i++){
            $m=Get-PurposeMatch $lines[$i] $ext
            if($m.Success){$idx=$i;$purpose=$m.Groups[1].Value.Trim();break}
        }
        if($idx -lt 0){continue}
        $broken=Test-BrokenPurposeText -Purpose $purpose -Candidates @(Get-Candidates $file.FullName $content)
        if(-not $broken){continue}
        $lines[$idx]=Get-PurposeLine $ext (Get-Replacement $file.FullName $content)
        [System.IO.File]::WriteAllText($file.FullName,(($lines -join "`n").TrimEnd("`n")+"`n"),$Utf8NoBom)
        $changed.Add((Get-RelativePath $file.FullName))|Out-Null
    }
}
Write-Host "Broken Purpose repair complete. Changed: $($changed.Count)" -ForegroundColor Cyan
$changed | ForEach-Object { Write-Host "[FIXED] $_" -ForegroundColor Green }
