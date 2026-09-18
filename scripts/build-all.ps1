# Purpose: Builds all first-party DIP components required for local verification.
param([ValidateSet("Debug","Release")][string]$Configuration="Release")
$ErrorActionPreference="Stop"
dotnet restore
dotnet build --configuration $Configuration --no-restore
$python="services/python-analysis/.venv/Scripts/python.exe"; if(-not(Test-Path $python)){$python="python"}
& $python -m compileall services/python-analysis/app services/python-analysis/tests
cmake -S collectors/native-collector -B collectors/native-collector/build -DBUILD_TESTING=ON
cmake --build collectors/native-collector/build --config $Configuration --parallel
