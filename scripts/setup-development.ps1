param([switch]$SkipInstall,[switch]$SkipDocker)
$ErrorActionPreference="Stop"
Set-StrictMode -Version Latest
if(-not $SkipInstall){
  $ids=@("Git.Git","GitHub.cli","Microsoft.DotNet.SDK.8","Python.Python.3.12","Kitware.CMake")
  if(-not $SkipDocker){$ids+="Docker.DockerDesktop"}
  foreach($id in $ids){winget install --id $id -e --accept-package-agreements --accept-source-agreements}
}
foreach($cmd in @("git","dotnet","python","cmake")){if(-not(Get-Command $cmd -ErrorAction SilentlyContinue)){throw "Missing command: $cmd"}}
if(-not $SkipDocker -and -not(Get-Command docker -ErrorAction SilentlyContinue)){throw "Missing command: docker"}
if(-not(Test-Path ".config/dotnet-tools.json")){dotnet new tool-manifest}
dotnet tool install dotnet-ef --version 8.* 2>$null
if($LASTEXITCODE -ne 0){dotnet tool update dotnet-ef --version 8.*}
if(Test-Path "services/python-analysis"){
  Push-Location "services/python-analysis"
  try{
    if(-not(Test-Path ".venv")){python -m venv .venv}
    & ".\.venv\Scripts\python.exe" -m pip install --upgrade pip
    & ".\.venv\Scripts\python.exe" -m pip install -r requirements.txt
    if(Test-Path "requirements-dev.txt"){& ".\.venv\Scripts\python.exe" -m pip install -r requirements-dev.txt}
  }finally{Pop-Location}
}
Write-Host "Development setup completed."
