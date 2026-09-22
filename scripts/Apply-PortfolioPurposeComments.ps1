#requires -Version 5.1
# Purpose: Replaces generic portfolio-critical Purpose comments with file-specific responsibility descriptions.
[CmdletBinding()]
param([string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)
$ErrorActionPreference = 'Stop'
$generic = 'Purpose: Defines a focused responsibility within the development intelligence platform.'
$changes = @(
    @{ Path = 'src/Dip.Application/Features/P2Intelligence/AdaptiveQaPlanner.cs'; Purpose = '// Purpose: Plans adaptive QA work from project risk, repository signals, and prior execution evidence.' }
    @{ Path = 'src/Dip.Application/Features/QualityAutomation/ProductionQaIntelligence.cs'; Purpose = '// Purpose: Classifies production-oriented QA domains, capabilities, findings, and media regression configuration.' }
    @{ Path = 'plugins/Maya/DipMayaPlugin/dip_maya_plugin.py'; Purpose = '# Purpose: Bridges Maya production events to the local DIP agent without storing API credentials in the DCC plugin.' }
    @{ Path = 'collectors/native-collector/src/AssetHasher.cpp'; Purpose = '// Purpose: Computes stable content hashes for native collector assets used in duplicate and evidence analysis.' }
    @{ Path = 'services/python-analysis/app/services/risk_analyzer.py'; Purpose = '# Purpose: Evaluates evidence-backed project risk contributions without treating model output as authoritative truth.' }
    @{ Path = 'collectors/native-collector/include/dip/AssetHasher.hpp'; Purpose = '// Purpose: Declares native collector asset hashing used to bind file content to reproducible evidence.' }
    @{ Path = 'src/Dip.Api/Authorization/Policies.cs'; Purpose = '// Purpose: Defines named authorization policies for project read, write, administration, and production operations.' }
    @{ Path = 'services/python-analysis/app/services/anomaly_detector.py'; Purpose = '# Purpose: Detects statistical anomalies in project time-series evidence for advisory analysis.' }
    @{ Path = 'src/Dip.Api/Authorization/ProductionSecurityGuard.cs'; Purpose = '// Purpose: Enforces fail-closed production security preconditions before privileged production operations proceed.' }
    @{ Path = 'src/Dip.Api/Authorization/ProjectScopeAuthorization.cs'; Purpose = '// Purpose: Resolves and enforces project-scoped authorization from authenticated server-side identity.' }
    @{ Path = 'src/Dip.Application/Features/QualityAutomation/AssetQa/AssetQaRuleEngine.cs'; Purpose = '// Purpose: Evaluates asset QA rules and converts asset integrity violations into normalized QA findings.' }
    @{ Path = 'services/python-analysis/app/services/log_classifier.py'; Purpose = '# Purpose: Classifies development log messages into normalized categories for downstream evidence analysis.' }
    @{ Path = 'services/python-analysis/app/services/evidence_builder.py'; Purpose = '# Purpose: Builds normalized evidence items from analysis inputs while preserving source and revision context.' }
    @{ Path = 'src/Dip.Application/Features/QualityAutomation/VisualQa/VisualComparator.cs'; Purpose = '// Purpose: Compares visual QA inputs and emits deterministic image-difference measurements and findings.' }
    @{ Path = 'src/Dip.Application/Features/QualityAutomation/VisualQa/PortablePngCodec.cs'; Purpose = '// Purpose: Provides portable PNG decoding and encoding primitives used by visual regression QA.' }
    @{ Path = 'services/python-analysis/app/services/release_predictor.py'; Purpose = '# Purpose: Produces evidence-grounded advisory release predictions from project signals without acting as a release gate.' }
    @{ Path = 'services/python-analysis/app/services/summary_generator.py'; Purpose = '# Purpose: Produces bounded evidence summaries for project analysis while retaining source references.' }
    @{ Path = 'collectors/native-collector/src/main.cpp'; Purpose = '// Purpose: Hosts the native collector command-line entry point and orchestrates scanning, analysis, and report emission.' }
    @{ Path = 'collectors/native-collector/src/LogAnalyzer.cpp'; Purpose = '// Purpose: Analyzes collected native logs and emits normalized diagnostics for evidence ingestion.' }
    @{ Path = 'src/Dip.Application/Features/ProjectTruth/ProgressEvaluator.cs'; Purpose = '// Purpose: Evaluates project progress claims against revision-bound evidence and explicit verification state.' }
    @{ Path = 'collectors/native-collector/src/FileScanner.cpp'; Purpose = '// Purpose: Scans configured project files for native collector hashing, metadata, and duplicate analysis.' }
    @{ Path = 'src/Dip.Application/Features/QualityAutomation/QaFingerprint.cs'; Purpose = '// Purpose: Creates stable QA finding fingerprints from normalized language, rule, location, and diagnostic data.' }
    @{ Path = 'src/Dip.Application/Features/QualityAutomation/QaCapabilities.cs'; Purpose = '// Purpose: Defines capability planning, semantic analysis, and cross-cutting QA contracts used by universal QA execution.' }
)
$updated = 0
foreach ($item in $changes) {
    $path = Join-Path $RepositoryRoot $item.Path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing expected file: $($item.Path)" }
    $text = [System.IO.File]::ReadAllText($path)
    $lines = $text -split "`r?`n", 2
    if ($lines.Count -lt 1) { throw "Empty source file: $($item.Path)" }
    if ($lines[0] -notmatch [regex]::Escape($generic)) {
        throw "Expected generic Purpose comment was not found at line 1: $($item.Path)"
    }
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $rest = if ($lines.Count -gt 1) { $lines[1] } else { '' }
    [System.IO.File]::WriteAllText($path, $item.Purpose + $newline + $rest, (New-Object System.Text.UTF8Encoding($false)))
    $updated++
}
Write-Host "Updated $updated portfolio-critical Purpose comments." -ForegroundColor Green
