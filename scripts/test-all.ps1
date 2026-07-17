param([ValidateSet("Debug","Release")][string]$Configuration="Release",[switch]$SkipIntegration,[switch]$SkipLoad)
$ErrorActionPreference="Stop"
New-Item -ItemType Directory -Force -Path "reports/test-results"|Out-Null
$args=@("test","--configuration",$Configuration,"--logger","trx;LogFileName=dotnet-tests.trx","--results-directory","reports/test-results/dotnet")
if($SkipIntegration){$args+=@("--filter","FullyQualifiedName!~Integration&FullyQualifiedName!~EndToEnd")}
dotnet @args
$python="services/python-analysis/.venv/Scripts/python.exe"; if(-not(Test-Path $python)){$python="python"}
& $python -m pytest services/python-analysis/tests --junitxml="reports/test-results/python-tests.xml"
cmake -S collectors/native-collector -B collectors/native-collector/build -DBUILD_TESTING=ON
cmake --build collectors/native-collector/build --config $Configuration
ctest --test-dir collectors/native-collector/build -C $Configuration --output-on-failure
if(-not $SkipLoad -and (Get-Command k6 -ErrorAction SilentlyContinue)){k6 run --vus 1 --duration 5s tests/load/k6/dashboard-load.js}
