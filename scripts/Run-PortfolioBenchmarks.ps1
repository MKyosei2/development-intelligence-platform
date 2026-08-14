[CmdletBinding()]
param(
    [int]$ProjectCount = 250,
    [int]$ProjectListIterations = 50,
    [int]$CollectorIterations = 30,
    [int]$MetricQueryIterations = 10,
    [switch]$UpdateDocumentation
)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out=Join-Path $root 'artifacts\portfolio-benchmark';New-Item -ItemType Directory -Force -Path $out|Out-Null
foreach($cmd in @('git','dotnet','docker')){if(-not(Get-Command $cmd -ErrorAction SilentlyContinue)){throw "$cmd is required."}}
Add-Type -AssemblyName System.Net.Http
function Get-FreePort{$l=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,0);$l.Start();$p=([Net.IPEndPoint]$l.LocalEndpoint).Port;$l.Stop();$p}
function Get-P95([double[]]$Values){if($Values.Count-eq0){return 0};$s=@($Values|Sort-Object);$i=[Math]::Max(0,[Math]::Ceiling($s.Count*0.95)-1);[Math]::Round($s[$i],3)}
function Wait-Http([string]$Url){$deadline=(Get-Date).AddSeconds(90);do{try{$r=Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3;if($r.StatusCode-eq200){return}}catch{};Start-Sleep -Milliseconds 500}while((Get-Date)-lt$deadline);throw "Timed out waiting for $Url"}
$sha=(& git -C $root rev-parse HEAD|Out-String).Trim();$stamp=Get-Date -Format 'yyyyMMddHHmmss';$compose="dip-bench-$stamp"
$pgPort=Get-FreePort;$redisPort=Get-FreePort;$apiPort=Get-FreePort;$apiBase="http://127.0.0.1:$apiPort";$apiProcess=$null
$names=@('POSTGRES_PORT','REDIS_PORT','POSTGRES_DB','POSTGRES_USER','POSTGRES_PASSWORD','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','ConnectionStrings__PostgreSql','Redis__ConnectionString','Qa__WorkspaceRoot','Qa__ArtifactRoot')
$old=@{};foreach($n in $names){$old[$n]=[Environment]::GetEnvironmentVariable($n,'Process')}
try{
  $env:POSTGRES_PORT="$pgPort";$env:REDIS_PORT="$redisPort";$env:POSTGRES_DB='dipbench';$env:POSTGRES_USER='dip';$env:POSTGRES_PASSWORD='dip-benchmark-password'
  Push-Location $root
  try{
    & docker compose -p $compose up -d postgres redis;if($LASTEXITCODE -ne 0){throw 'Failed to start PostgreSQL/Redis.'}
    $ready=$false
    for($attempt=1;$attempt -le 60;$attempt++){
      & docker compose -p $compose exec -T postgres pg_isready -U dip -d dipbench *> $null
      if($LASTEXITCODE -eq 0){$ready=$true;break}
      Start-Sleep -Milliseconds 500
    }
    if(-not $ready){throw 'PostgreSQL did not become ready.'}
    & dotnet build DevelopmentIntelligencePlatform.sln -c Release --nologo;if($LASTEXITCODE -ne 0){throw 'Release build failed.'}
  } finally {Pop-Location}
  $env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS=$apiBase;$env:ConnectionStrings__PostgreSql="Host=127.0.0.1;Port=$pgPort;Database=dipbench;Username=dip;Password=dip-benchmark-password";$env:Redis__ConnectionString="127.0.0.1:$redisPort";$env:Qa__WorkspaceRoot=$root;$env:Qa__ArtifactRoot=(Join-Path $root 'artifacts\qa')
  $dll=Join-Path $root 'src\Dip.Api\bin\Release\net8.0\Dip.Api.dll';$apiProcess=Start-Process -FilePath 'dotnet' -ArgumentList @($dll) -WorkingDirectory $root -PassThru -WindowStyle Hidden
  Wait-Http "$apiBase/health"
  $project=$null
  for($i=0;$i-lt$ProjectCount;$i++){
    $body=@{name="Portfolio Benchmark $i";repositoryUrl="https://github.com/example/dip-portfolio-benchmark-$i";defaultBranch='main';releaseDateUtc=$null}|ConvertTo-Json
    $created=Invoke-RestMethod -Method Post -Uri "$apiBase/api/projects" -ContentType 'application/json' -Body $body
    if($i -eq 0){$project=$created}
  }
  1..10|ForEach-Object{Invoke-WebRequest -UseBasicParsing -Uri "$apiBase/api/projects"|Out-Null}
  $projectMs=@();1..$ProjectListIterations|ForEach-Object{$sw=[Diagnostics.Stopwatch]::StartNew();$r=Invoke-WebRequest -UseBasicParsing -Uri "$apiBase/api/projects" -TimeoutSec 20;$sw.Stop();if($r.StatusCode -ne 200){throw 'Project list request failed.'};$projectMs+=$sw.Elapsed.TotalMilliseconds}
  $collectorMs=@();$lastPipeline=$null
  1..$CollectorIterations|ForEach-Object{
    $now=[DateTimeOffset]::UtcNow
    $payload=@{schemaVersion='1.0';reportId="bench-report-$stamp-$($_)-$([Guid]::NewGuid().ToString('N'))";externalRunId="bench-run-$stamp-$($_)-$([Guid]::NewGuid().ToString('N'))";attemptNumber=1;collectorType='portfolio-benchmark';collectorVersion='1.0.0';projectId=$project.id;repository=$project.repositoryUrl;commitSha='0123456789abcdef0123456789abcdef01234567';branch='main';startedAt=$now.AddSeconds(-1);finishedAt=$now;status='Succeeded';metrics=@(@{name='frame_time';value=16.0;unit='ms';baselineValue=16.6;environmentId='portfolio';scenarioId='benchmark';platform='windows';buildConfiguration='Release';benchmarkVersion='1'});issues=@()}|ConvertTo-Json -Depth 8
    $sw=[Diagnostics.Stopwatch]::StartNew();$response=Invoke-RestMethod -Method Post -Uri "$apiBase/api/v1/collector-reports" -ContentType 'application/json' -Body $payload;$sw.Stop();$collectorMs+=$sw.Elapsed.TotalMilliseconds;$lastPipeline=$response.pipelineRunId
  }
  if(-not $lastPipeline){throw 'Collector benchmark did not return a PipelineRunId.'}
  $client=New-Object Net.Http.HttpClient;$tasks=New-Object System.Collections.ArrayList;$burst=[Diagnostics.Stopwatch]::StartNew()
  for($i=0;$i-lt100;$i++){while($burst.Elapsed.TotalMilliseconds-lt($i*10)){Start-Sleep -Milliseconds 1};[void]$tasks.Add($client.GetAsync("$apiBase/api/projects"))}
  [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks.ToArray([Threading.Tasks.Task]));$burst.Stop();$errors=0;foreach($task in $tasks){if(-not$task.Result.IsSuccessStatusCode){$errors++};$task.Result.Dispose()};$client.Dispose();$errorRate=[Math]::Round($errors,3)
  $seed=@"
INSERT INTO "Metrics" ("Id","PipelineRunId","BaselinePipelineRunId","Name","Value","Unit","BaselineValue","Difference","DifferencePercent","Severity","ComparisonStatus","EnvironmentId","ScenarioId","Platform","BuildConfiguration","BenchmarkVersion")
SELECT gen_random_uuid(),'$lastPipeline'::uuid,NULL,'portfolio.metric.'||(g%100),(g%1000)::numeric,'ms',NULL,NULL,NULL,0,0,'portfolio','one-million','windows','Release','1' FROM generate_series(1,1000000) g;
"@
  Push-Location $root;try{$seed|& docker compose -p $compose exec -T postgres psql -U dip -d dipbench -v ON_ERROR_STOP=1|Out-Null;if($LASTEXITCODE -ne 0){throw '1M metric seed failed.'}}finally{Pop-Location}
  $metricMs=@();$query="SELECT \"Name\",AVG(\"Value\") FROM \"Metrics\" WHERE \"PipelineRunId\"='$lastPipeline'::uuid GROUP BY \"Name\" ORDER BY \"Name\";"
  1..$MetricQueryIterations|ForEach-Object{$sw=[Diagnostics.Stopwatch]::StartNew();Push-Location $root;try{$query|& docker compose -p $compose exec -T postgres psql -U dip -d dipbench -q -t -v ON_ERROR_STOP=1|Out-Null;if($LASTEXITCODE -ne 0){throw '1M metric query failed.'}}finally{Pop-Location};$sw.Stop();$metricMs+=$sw.Elapsed.TotalMilliseconds}
  $projectP95=Get-P95 $projectMs;$collectorP95=Get-P95 $collectorMs;$metricP95=Get-P95 $metricMs
  $result=[ordered]@{schema='dip-portfolio-benchmark-v1';sourceCommit=$sha;measuredAtUtc=[DateTimeOffset]::UtcNow.ToString('o');environment=[ordered]@{os=[Environment]::OSVersion.VersionString;machine=$env:COMPUTERNAME;processorCount=[Environment]::ProcessorCount;dotnet=(& dotnet --version|Out-String).Trim()};scenarios=[ordered]@{projectList=[ordered]@{population=$ProjectCount;iterations=$ProjectListIterations;p95Ms=$projectP95;targetMs=500;passed=$projectP95-lt500};collector=[ordered]@{iterations=$CollectorIterations;p95Ms=$collectorP95;targetMs=1000;passed=$collectorP95-lt1000};hundredRequestsPerSecond=[ordered]@{requests=100;elapsedMs=[Math]::Round($burst.Elapsed.TotalMilliseconds,3);errorRatePercent=$errorRate;targetPercent=1;passed=$errorRate-lt1};millionMetricQuery=[ordered]@{rows=1000000;iterations=$MetricQueryIterations;p95Ms=$metricP95;targetMs=2000;passed=$metricP95-lt2000}}}
  $jsonPath=Join-Path $out 'portfolio-benchmark-results.json';$result|ConvertTo-Json -Depth 10|Set-Content $jsonPath -Encoding UTF8
  $md=@("# Benchmark Results","","Measured evidence for commit ``$sha`` at $($result.measuredAtUtc).","","| Scenario | Target | Measured | Result |","|---|---:|---:|---|","| Project list p95 ($ProjectCount projects) | <500 ms | $projectP95 ms | $(if($result.scenarios.projectList.passed){'PASS'}else{'FAIL'}) |","| Collector p95 | <1000 ms | $collectorP95 ms | $(if($result.scenarios.collector.passed){'PASS'}else{'FAIL'}) |","| 100 req/s error rate | <1% | $errorRate% | $(if($result.scenarios.hundredRequestsPerSecond.passed){'PASS'}else{'FAIL'}) |","| 1M Metric query p95 | <2000 ms | $metricP95 ms | $(if($result.scenarios.millionMetricQuery.passed){'PASS'}else{'FAIL'}) |","","Raw evidence: ``artifacts/portfolio-benchmark/portfolio-benchmark-results.json``.") -join "`n"
  $md|Set-Content (Join-Path $out 'portfolio-benchmark-results.md') -Encoding UTF8;if($UpdateDocumentation){$md|Set-Content (Join-Path $root 'docs\09-portfolio\benchmark-results.md') -Encoding UTF8}
  Write-Host "Portfolio benchmark evidence: $jsonPath" -ForegroundColor Green
  if(-not ($result.scenarios.projectList.passed -and $result.scenarios.collector.passed -and $result.scenarios.hundredRequestsPerSecond.passed -and $result.scenarios.millionMetricQuery.passed)){exit 3}
}
finally{
  if($apiProcess -and-not$apiProcess.HasExited){Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue}
  Push-Location $root;try{& docker compose -p $compose down -v --remove-orphans *> $null}catch{}finally{Pop-Location}
  foreach($n in $names){[Environment]::SetEnvironmentVariable($n,$old[$n],'Process')}
}
