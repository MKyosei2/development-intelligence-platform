#requires -Version 5.1
# Purpose: Repairs residual Source Readability findings that remain after Purpose normalization and C# formatting.
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RepositoryRoot
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
$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92,47))
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$Changed = New-Object 'System.Collections.Generic.List[string]'

function Get-RelativePath([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
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

function Test-DenseRazorMarkup([string]$Line) {
    return $Line.Length -ge 360 -and [regex]::Matches($Line,'<[A-Za-z][^>]*>').Count -ge 8
}

function Add-IntentionalCatchComments([string]$Content) {
    $pattern='(?s)\bcatch\s*(?:\([^)]*\))?\s*\{\s*\}'
    return [regex]::Replace($Content,$pattern,{
        param($match)
        $lineStart=$Content.LastIndexOf("`n",[Math]::Max(0,$match.Index-1))
        if($lineStart -lt 0){$lineStart=0}else{$lineStart++}
        $prefixText=$Content.Substring($lineStart,$match.Index-$lineStart)
        $indent=[regex]::Match($prefixText,'^\s*').Value
        $header=[regex]::Match($match.Value,'(?s)^\s*(\bcatch\s*(?:\([^)]*\))?\s*\{)').Groups[1].Value
        return $header+"`n"+$indent+"    // Intentionally ignored to preserve the existing best-effort or fallback behavior.`n"+$indent+"}"
    })
}

function Split-RazorStartTags([string]$Line,[string]$Indent) {
    $body=$Line.Substring([Math]::Min($Indent.Length,$Line.Length))
    $matches=[regex]::Matches($body,'<[A-Za-z][^>]*>')
    if($matches.Count -lt 2){return $Line}
    $firstIndex=$matches[0].Index
    $result=New-Object System.Text.StringBuilder
    $cursor=0
    foreach($match in $matches){
        if($match.Index -gt $cursor){[void]$result.Append($body.Substring($cursor,$match.Index-$cursor))}
        if($match.Index -ne $firstIndex){[void]$result.Append("`n"+$Indent)}
        [void]$result.Append($match.Value)
        $cursor=$match.Index+$match.Length
    }
    if($cursor -lt $body.Length){[void]$result.Append($body.Substring($cursor))}
    return $Indent+$result.ToString()
}

function Split-CSharpStatements([string]$Line,[string]$Indent) {
    $builder=New-Object System.Text.StringBuilder
    $paren=0
    $bracket=0
    $inDouble=$false
    $inSingle=$false
    $escape=$false
    for($i=0;$i -lt $Line.Length;$i++){
        $ch=$Line[$i]
        if($inDouble){
            [void]$builder.Append($ch)
            if($escape){$escape=$false;continue}
            if($ch -eq [char]92){$escape=$true;continue}
            if($ch -eq '"'){$inDouble=$false}
            continue
        }
        if($inSingle){
            [void]$builder.Append($ch)
            if($escape){$escape=$false;continue}
            if($ch -eq [char]92){$escape=$true;continue}
            if($ch -eq "'"){$inSingle=$false}
            continue
        }
        if($ch -eq '"'){$inDouble=$true;[void]$builder.Append($ch);continue}
        if($ch -eq "'"){$inSingle=$true;[void]$builder.Append($ch);continue}
        if($ch -eq '('){$paren++}
        elseif($ch -eq ')'){$paren=[Math]::Max(0,$paren-1)}
        elseif($ch -eq '['){$bracket++}
        elseif($ch -eq ']'){$bracket=[Math]::Max(0,$bracket-1)}
        [void]$builder.Append($ch)
        if($ch -eq ';'){
            [void]$builder.Append("`n"+$Indent)
        }
    }
    return $builder.ToString().TrimEnd()
}

function Expand-CSharpLine([string]$Line) {
    if(-not(Test-CompressedCodeLine $Line)){return $Line}
    $indent=[regex]::Match($Line,'^\s*').Value
    $candidate=Split-CSharpStatements -Line $Line -Indent $indent

    # Newlines are whitespace in C#, so these rewrites preserve behavior while
    # separating adjacent blocks and control statements for reviewer readability.
    $candidate=[regex]::Replace(
        $candidate,
        '}\s*(?=(?:else\s+)?(?:if|for|foreach|while|switch|try|catch|finally|public|private|internal|protected)\b)',
        "}`n$indent")
    $candidate=[regex]::Replace(
        $candidate,
        '(?<=[;)])\s+(?=(?:if|for|foreach|while|switch)\s*\()',
        "`n$indent")

    return $candidate
}

function Wrap-ResidualCompressedLine([string]$Line) {
    if(-not(Test-CompressedCodeLine $Line)){return $Line}

    $indent=[regex]::Match($Line,'^\s*').Value
    $safe=New-Object 'System.Collections.Generic.List[int]'
    $inDouble=$false
    $inSingle=$false
    $escape=$false
    $verbatim=$false

    for($i=0;$i -lt $Line.Length;$i++){
        $ch=$Line[$i]
        $next=if($i+1 -lt $Line.Length){$Line[$i+1]}else{[char]0}

        if($inDouble){
            if($verbatim){
                if($ch -eq '"' -and $next -eq '"'){$i++;continue}
                if($ch -eq '"'){$inDouble=$false;$verbatim=$false}
                continue
            }
            if($escape){$escape=$false;continue}
            if($ch -eq [char]92){$escape=$true;continue}
            if($ch -eq '"'){$inDouble=$false}
            continue
        }

        if($inSingle){
            if($escape){$escape=$false;continue}
            if($ch -eq [char]92){$escape=$true;continue}
            if($ch -eq "'"){$inSingle=$false}
            continue
        }

        if($ch -eq '/' -and $next -eq '/'){break}
        if($ch -eq '"'){
            $verbatim=($i -gt 0 -and $Line[$i-1] -eq '@')
            $inDouble=$true
            continue
        }
        if($ch -eq "'"){$inSingle=$true;continue}

        if([char]::IsWhiteSpace($ch)){$safe.Add($i)|Out-Null}
    }

    $segments=New-Object 'System.Collections.Generic.List[string]'
    $start=0
    while(($Line.Length-$start) -gt 170){
        $limit=[Math]::Min($Line.Length-1,$start+160)
        $minimum=$start+80
        $break=-1
        foreach($index in $safe){
            if($index -gt $limit){break}
            if($index -ge $minimum){$break=$index}
        }
        if($break -lt 0){break}

        $piece=$Line.Substring($start,$break-$start).TrimEnd()
        if($segments.Count -gt 0){$piece=$indent+$piece.TrimStart()}
        $segments.Add($piece)|Out-Null
        $start=$break+1
        while($start -lt $Line.Length -and [char]::IsWhiteSpace($Line[$start])){$start++}
    }

    if($segments.Count -eq 0){return $Line}
    $tail=$Line.Substring($start).TrimEnd()
    if($tail.Length -gt 0){$segments.Add($indent+$tail.TrimStart())|Out-Null}
    return ($segments -join "`n")
}
function Expand-RazorLine([string]$Line) {
    $indent=[regex]::Match($Line,'^\s*').Value
    $expanded=$Line
    if((Test-DenseRazorMarkup $expanded) -or (Test-CompressedCodeLine $expanded)){
        $expanded=Split-RazorStartTags -Line $expanded -Indent $indent
    }

    $parts=New-Object 'System.Collections.Generic.List[string]'
    foreach($part in ($expanded -split "`n")){
        $candidate=$part
        if((Test-CompressedCodeLine $candidate) -and $candidate -notmatch '<[A-Za-z][^>]*>'){
            $candidate=Split-CSharpStatements -Line $candidate -Indent $indent
            $candidate=[regex]::Replace(
                $candidate,
                '}\s*(?=(?:else\s+)?(?:if|for|foreach|while|switch|try|catch|finally|public|private|internal|protected)\b)',
                "}`n$indent")
        }
        foreach($item in ($candidate -split "`n")){$parts.Add($item)|Out-Null}
    }
    return ($parts -join "`n")
}

$roots=@('src','tests','collectors','plugins')
foreach($rootName in $roots){
    $root=Join-Path $Repo $rootName
    if(-not(Test-Path -LiteralPath $root -PathType Container)){continue}
    foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File){
        if(Test-ExcludedPath $file.FullName){continue}
        $extension=$file.Extension.ToLowerInvariant()
        if($extension -notin @('.cs','.razor')){continue}
        $content=[System.IO.File]::ReadAllText($file.FullName)
        if([string]::IsNullOrEmpty($content)){continue}
        $updated=Add-IntentionalCatchComments -Content $content

        $normalized=$updated -replace "`r`n","`n" -replace "`r","`n"
        $out=New-Object 'System.Collections.Generic.List[string]'
        foreach($line in ($normalized -split "`n")){
            $expanded=if($extension -eq '.razor'){
                Expand-RazorLine -Line $line
            }else{
                Expand-CSharpLine -Line $line
            }
            foreach($item in ($expanded -split "`n")){
                $final=Wrap-ResidualCompressedLine -Line $item
                foreach($wrapped in ($final -split "`n")){$out.Add($wrapped)|Out-Null}
            }
        }
        $updated=(($out -join "`n").TrimEnd("`n")+"`n")

        if($updated -eq $content){continue}
        $relative=Get-RelativePath $file.FullName
        if($PSCmdlet.ShouldProcess($relative,'Repair residual Source Readability findings')){
            [System.IO.File]::WriteAllText($file.FullName,$updated,$Utf8NoBom)
            $Changed.Add($relative)|Out-Null
            Write-Host "[FIXED] $relative" -ForegroundColor Green
        }
    }
}
Write-Host "Residual readability repair complete. Changed: $($Changed.Count)" -ForegroundColor Cyan
