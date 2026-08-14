[CmdletBinding()]
param([int]$WebPort=5081,[int]$ApiPort=5080,[int]$AnalysisPort=8100,[int]$WorkerMetricsPort=9091)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envFile=Join-Path $root 'artifacts\runtime\dip.runtime.env'
$args=@('compose');if(Test-Path $envFile){$args+=@('--env-file',$envFile)};$args+='ps'
& docker @args
Write-Host ''
foreach($item in @(
    @('Web GUI',"http://127.0.0.1:$WebPort/"),
    @('API',"http://127.0.0.1:$ApiPort/health"),
    @('Analysis',"http://127.0.0.1:$AnalysisPort/health"),
    @('Worker',"http://127.0.0.1:$WorkerMetricsPort/metrics")
)){
    try{$r=Invoke-WebRequest $item[1] -UseBasicParsing -TimeoutSec 3;Write-Host ('{0,-12} PASS  {1}' -f $item[0],$item[1]) -ForegroundColor Green}catch{Write-Host ('{0,-12} FAIL  {1}' -f $item[0],$item[1]) -ForegroundColor Red}
}
