# Purpose: Automates the f or ma t-a ll repository workflow.
param([switch]$Check)
$ErrorActionPreference="Stop"
if($Check){dotnet format --verify-no-changes}else{dotnet format}
$python="services/python-analysis/.venv/Scripts/python.exe"; if(-not(Test-Path $python)){$python="python"}
& $python -m ruff check services/python-analysis/app services/python-analysis/tests services/python-analysis/scripts
if($Check){& $python -m ruff format --check services/python-analysis/app services/python-analysis/tests services/python-analysis/scripts}
else{& $python -m ruff check --fix services/python-analysis/app services/python-analysis/tests services/python-analysis/scripts; & $python -m ruff format services/python-analysis/app services/python-analysis/tests services/python-analysis/scripts}
