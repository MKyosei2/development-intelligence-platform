#requires -Version 5.1
# Purpose: Defines shared, side-effect-free rules for detecting genuinely fragmented Purpose identifiers.
Set-StrictMode -Version Latest

function ConvertTo-PurposeIdentifier {
    param([AllowNull()][string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return '' }
    return (($Value -replace '[^A-Za-z0-9]', '').ToLowerInvariant())
}

function Get-PurposeIdentifierVariants {
    param([Parameter(Mandatory = $true)][string]$Candidate)

    $values = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $normalized = ConvertTo-PurposeIdentifier $Candidate
    if ($normalized.Length -ge 3) { [void]$values.Add($normalized) }

    $withoutSuffix = $Candidate -replace '(Tests?|Repository|Controller|Worker|Job|Service|Dto|DTO|Request|Response|Contract|Options|Configuration|Extensions)$',''
    $normalizedSubject = ConvertTo-PurposeIdentifier $withoutSuffix
    if ($normalizedSubject.Length -ge 3) { [void]$values.Add($normalizedSubject) }

    return @($values | ForEach-Object { [string]$_ })
}

function Test-FragmentedPurposeCandidate {
    param(
        [Parameter(Mandatory = $true)][string]$Purpose,
        [Parameter(Mandatory = $true)][string]$Candidate
    )

    $variants = @(Get-PurposeIdentifierVariants -Candidate $Candidate)
    if ($variants.Count -eq 0) { return $false }

    $tokens = @([regex]::Matches($Purpose, '[A-Za-z0-9]+') | ForEach-Object { $_.Value })
    for ($start = 0; $start -lt $tokens.Count; $start++) {
        if ($tokens[$start].Length -gt 2) { continue }

        $joined = ''
        $parts = 0
        for ($index = $start; $index -lt $tokens.Count; $index++) {
            $token = $tokens[$index]
            if ($token.Length -gt 2) { break }
            $joined += $token.ToLowerInvariant()
            $parts++

            if ($parts -lt 3) { continue }
            foreach ($variant in $variants) {
                if ($joined -ceq $variant) { return $true }
            }
        }
    }

    return $false
}

function Test-BrokenPurposeText {
    param(
        [Parameter(Mandatory = $true)][string]$Purpose,
        [Parameter(Mandatory = $true)][string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-FragmentedPurposeCandidate -Purpose $Purpose -Candidate $candidate) { return $true }
    }
    return $false
}
