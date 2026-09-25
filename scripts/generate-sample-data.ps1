# Purpose: Runs generate sample data as part of repository development and verification workflows.
param([string]$OutputDirectory="artifacts/sample-data")
$ErrorActionPreference="Stop"
New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
Copy-Item "samples/sample-pipeline-data/*.json" $OutputDirectory -Force
Copy-Item "samples/sample-game-project/collector-reports/*.json" $OutputDirectory -Force
$manifest=Get-ChildItem $OutputDirectory -File|ForEach-Object{[PSCustomObject]@{name=$_.Name;sizeBytes=$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}}
$manifest|ConvertTo-Json -Depth 4|Set-Content "$OutputDirectory/manifest.json" -Encoding utf8
