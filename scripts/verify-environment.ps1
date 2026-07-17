$ErrorActionPreference="Stop"
$checks=@(
  @("Git","git","--version"),@("GitHub CLI","gh","--version"),@(".NET","dotnet","--version"),
  @("Python","python","--version"),@("Docker","docker","--version"),@("CMake","cmake","--version")
)
$failed=$false
foreach($c in $checks){
  try{$o=& $c[1] $c[2] 2>&1; Write-Host "[OK] $($c[0]): $($o|Select-Object -First 1)"}
  catch{Write-Host "[NG] $($c[0])" -ForegroundColor Red;$failed=$true}
}
foreach($p in @("src/Dip.Api/Dip.Api.csproj","services/python-analysis/app/main.py","collectors/native-collector/CMakeLists.txt","schemas/collector-report.schema.json")){
  if(Test-Path $p){Write-Host "[OK] $p"}else{Write-Host "[NG] $p" -ForegroundColor Red;$failed=$true}
}
if($failed){throw "Environment verification failed."}
