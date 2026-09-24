#requires -Version 5.1
# Purpose: Regression-tests Purpose fragmentation rules so acronyms remain valid while split identifiers are rejected.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SourcePurposeRules.ps1')

$cases = @(
    @{ Name='split GitHub type'; Purpose='Implements G it Hu bD ep en de nc yI nj ec ti on infrastructure integration.'; Candidate='GitHubDependencyInjection'; Expected=$true },
    @{ Name='split controller subject'; Purpose='Exposes A rt if ac ts HTTP endpoints for c on tr ol le rs.'; Candidate='ArtifactsController'; Expected=$true },
    @{ Name='split Python tool name'; Purpose='Implements f in al iz e p or tf ol io e vi de nc e tooling.'; Candidate='finalize_portfolio_evidence'; Expected=$true },
    @{ Name='split P2 jobs subject'; Purpose='Exposes P2 Jo bs HTTP endpoints for controllers.'; Candidate='P2JobsController'; Expected=$true },
    @{ Name='normal acronyms'; Purpose='Coordinates QA, API, CI, and P2 evidence for release verification.'; Candidate='ReleaseEvidenceService'; Expected=$false },
    @{ Name='normal short words'; Purpose='Runs CI QA checks in a fail-closed release gate.'; Candidate='QualityGateRunner'; Expected=$false },
    @{ Name='normal explicit name'; Purpose='Validates GitHub dependency injection registration for repository evidence.'; Candidate='GitHubDependencyInjection'; Expected=$false }
)

$failures = New-Object 'System.Collections.Generic.List[string]'
foreach ($case in $cases) {
    $actual = Test-FragmentedPurposeCandidate -Purpose $case.Purpose -Candidate $case.Candidate
    if ($actual -ne $case.Expected) {
        $failures.Add("$($case.Name): expected=$($case.Expected) actual=$actual") | Out-Null
    }
}

if ($failures.Count -gt 0) {
    throw "Purpose tooling regression failed:`n$($failures -join "`n")"
}
Write-Host "PASS: $($cases.Count) Purpose fragmentation regression cases passed." -ForegroundColor Green
