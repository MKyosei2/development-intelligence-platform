# Purpose: Automates the p ac ka ge-r el ea se repository workflow.
param([Parameter(Mandatory=$true)][string]$Version,[ValidateSet("Debug","Release")][string]$Configuration="Release")
$ErrorActionPreference="Stop"
if($Version -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$'){throw "Invalid semantic version."}
$root="artifacts/releases/$Version"; if(Test-Path $root){Remove-Item $root -Recurse -Force}; New-Item -ItemType Directory -Force -Path $root|Out-Null
dotnet publish src/Dip.Api/Dip.Api.csproj -c $Configuration -o "$root/api" -p:Version=$Version
dotnet publish src/Dip.Web/Dip.Web.csproj -c $Configuration -o "$root/web" -p:Version=$Version
dotnet publish src/Dip.Worker/Dip.Worker.csproj -c $Configuration -o "$root/worker" -p:Version=$Version
Copy-Item "schemas" "$root/schemas" -Recurse
$manifest=Get-ChildItem $root -Recurse -File|ForEach-Object{[PSCustomObject]@{path=$_.FullName.Substring((Resolve-Path $root).Path.Length+1).Replace('\','/');sizeBytes=$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}}
$manifest|ConvertTo-Json -Depth 4|Set-Content "$root/release-manifest.json" -Encoding utf8
Compress-Archive -Path "$root/*" -DestinationPath "artifacts/dip-$Version.zip" -Force
