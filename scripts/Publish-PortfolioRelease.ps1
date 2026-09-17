# Purpose: Publishes a versioned portfolio release after the publication gate succeeds.
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Version = "1.0.0-portfolio",
    [string]$Repository = "MKyosei2/development-intelligence-platform"
)
$ErrorActionPreference="Stop"
if($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$'){throw "Invalid semantic version: $Version"}
& "$PSScriptRoot/Publish-Portfolio.ps1" -Repository $Repository
if($LASTEXITCODE -ne 0){throw "Portfolio publication gate failed."}
$repoState=gh repo view $Repository --json visibility | ConvertFrom-Json
if($repoState.visibility -ne "PUBLIC"){throw "Repository must be public before publishing a portfolio release."}
$tag="v$Version"
if(git tag --list $tag){throw "Tag already exists: $tag"}
if($PSCmdlet.ShouldProcess($tag,"create and push signed/annotated portfolio release tag")){
    git tag -a $tag -m "Portfolio release $tag"
    git push origin $tag
    if($LASTEXITCODE -ne 0){throw "Failed to push $tag."}
    Write-Host "$tag pushed. The Release workflow will build and publish GitHub Release artifacts."
}
