$ErrorActionPreference = "Continue"
Set-StrictMode -Version Latest

$Repo = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Repo

$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ReportRoot = Join-Path $Repo "artifacts\full-ci-final\$Stamp"
New-Item -ItemType Directory -Force -Path $ReportRoot | Out-Null

$Results = New-Object System.Collections.Generic.List[object]
$DockerContainer = "dip-local-ci-postgres"
$DockerPostgresStarted = $false

function Add-Result {
    param([string]$Stage,[string]$Status,[string]$Detail,[string]$LogPath="")
    [void]$script:Results.Add([PSCustomObject]@{
        Stage=$Stage;Status=$Status;Detail=$Detail;Log=$LogPath
    })
    $color="Yellow"
    if($Status -eq "PASS"){$color="Green"}
    elseif($Status -eq "FAIL"){$color="Red"}
    Write-Host "[$Status] $Stage - $Detail" -ForegroundColor $color
}

function Invoke-Stage {
    param([string]$Stage,[scriptblock]$Command,[string]$LogName)
    $LogPath=Join-Path $script:ReportRoot $LogName
    try{
        $global:LASTEXITCODE=0
        & $Command *>&1 | Tee-Object -FilePath $LogPath
        $code=$LASTEXITCODE
        if($null -eq $code){$code=0}
        if($code -eq 0){
            Add-Result $Stage "PASS" "ExitCode=0" $LogPath
            return $true
        }
        Add-Result $Stage "FAIL" "ExitCode=$code" $LogPath
        return $false
    }
    catch{
        $_ | Out-String | Add-Content -Path $LogPath -Encoding UTF8
        Add-Result $Stage "FAIL" $_.Exception.Message $LogPath
        return $false
    }
}

function Has-Command([string]$Name){
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Test-TcpPort([string]$HostName,[int]$Port,[int]$TimeoutMs=1500){
    $client=New-Object System.Net.Sockets.TcpClient
    try{
        $async=$client.BeginConnect($HostName,$Port,$null,$null)
        if(-not $async.AsyncWaitHandle.WaitOne($TimeoutMs,$false)){return $false}
        $client.EndConnect($async)
        return $true
    }catch{return $false}finally{$client.Close()}
}

# 0. Apply deterministic source cleanup first, then test everything.
try{
    & (Join-Path $Repo "scripts\Apply-CIFirstGreenFix.ps1")
    Add-Result "Source Cleanup" "PASS" "Zero-byte Razor stubs removed"
}catch{
    Add-Result "Source Cleanup" "FAIL" $_.Exception.Message
}

# Fail locally before consuming hosted CI if first-party source is unreadable.
Invoke-Stage "Source Readability" {
    & (Join-Path $Repo "scripts\Verify-SourceReadability.ps1") -Strict
} "01-source-readability.log" | Out-Null

$Sha=(git rev-parse HEAD).Trim()
$changes=@(git status --porcelain)
if($changes.Count -eq 0){Add-Result "Git Working Tree" "PASS" "Clean; SHA=$Sha"}
else{Add-Result "Git Working Tree" "WARN" "$($changes.Count) modified/untracked entries; SHA=$Sha"}

$required=@(
    "README.md",
    "DevelopmentIntelligencePlatform.sln",
    "tools\verify_publication_safety.py",
    "tools\validate_portfolio_readiness.py",
    "scripts\Verify-SourceReadability.ps1",
    "scripts\Verify-SelfHostedWindowsRuntime.ps1",
    "scripts\Verify-GuiRuntime.ps1",
    "scripts\Verify-DatabaseMigrations.ps1",
    "scripts\Verify-DisasterRecovery.ps1"
)
$missing=@($required | Where-Object{-not(Test-Path $_)})
if($missing.Count -eq 0){Add-Result "Required Files" "PASS" "All present"}
else{Add-Result "Required Files" "FAIL" ($missing -join ", ")}

Invoke-Stage "Commit Message" {
    $subject=git log -1 --format=%s
    $subject | python tools/validate_commit_message.py
} "03-commit-message.log" | Out-Null

Invoke-Stage "Publication Safety" {
    python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
} "04-publication-safety.log" | Out-Null

Invoke-Stage "Portfolio Static Readiness" {
    python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
} "05-portfolio-readiness.log" | Out-Null

$empty=@(Get-ChildItem src\Dip.Web -Recurse -Filter *.razor -File -ErrorAction SilentlyContinue | Where-Object Length -eq 0)
if($empty.Count -eq 0){Add-Result "Empty Razor Files" "PASS" "0 files"}
else{Add-Result "Empty Razor Files" "FAIL" "$($empty.Count) empty files"}

$restore=Invoke-Stage ".NET Restore" {
    dotnet restore DevelopmentIntelligencePlatform.sln
} "07-dotnet-restore.log"

$build=$false
if($restore){
    $build=Invoke-Stage ".NET Release Build" {
        dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
    } "08-dotnet-build.log"
}else{Add-Result ".NET Release Build" "BLOCKED" "Restore failed"}

if($build){
    Invoke-Stage "Domain Tests" {
        dotnet test tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj -c Release --no-build
    } "09-domain-tests.log" | Out-Null

    Invoke-Stage "Application Tests" {
        dotnet test tests/Dip.Application.Tests/Dip.Application.Tests.csproj -c Release --no-build
    } "10-application-tests.log" | Out-Null

    Invoke-Stage "Infrastructure Tests" {
        dotnet test tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj -c Release --no-build
    } "11-infrastructure-tests.log" | Out-Null
}else{
    Add-Result "Domain Tests" "BLOCKED" "Build failed"
    Add-Result "Application Tests" "BLOCKED" "Build failed"
    Add-Result "Infrastructure Tests" "BLOCKED" "Build failed"
}

Invoke-Stage "Python Package/Compile/Pytest" {
    Push-Location services\python-analysis
    try{
        python -m pip install -e ".[dev]"
        if($LASTEXITCODE -ne 0){throw "python package install failed"}
        python -m compileall app tests
        if($LASTEXITCODE -ne 0){throw "python compileall failed"}
        python -m pytest --junitxml=python-test-results.xml
    }finally{Pop-Location}
} "14-python-tests.log" | Out-Null

Invoke-Stage "Unreal Collector" {
    python -m compileall collectors/unreal-collector
    if($LASTEXITCODE -ne 0){throw "Unreal compileall failed"}
    python -m unittest discover -s collectors/unreal-collector/tests -v
} "16-unreal-tests.log" | Out-Null

if((Has-Command cmake) -and (Has-Command ctest)){
    Invoke-Stage "Native Collector" {
        cmake -S collectors/native-collector -B build/native-local-ci -DBUILD_TESTING=ON
        if($LASTEXITCODE -ne 0){throw "Native configure failed"}
        cmake --build build/native-local-ci --config Release --parallel
        if($LASTEXITCODE -ne 0){throw "Native build failed"}
        ctest --test-dir build/native-local-ci -C Release --output-on-failure
    } "19-native-tests.log" | Out-Null
}else{Add-Result "Native Collector" "BLOCKED" "cmake/ctest missing"}

if($restore){
    Invoke-Stage "NuGet Vulnerability Audit" {
        $output=dotnet list DevelopmentIntelligencePlatform.sln package --vulnerable --include-transitive 2>&1
        $code=$LASTEXITCODE
        $output | Write-Output
        if($code -ne 0){throw "dotnet package audit failed"}
        if(($output | Out-String) -match "has the following vulnerable packages"){throw "Vulnerable NuGet package detected"}
        $global:LASTEXITCODE=0
    } "20-nuget-audit.log" | Out-Null
}else{Add-Result "NuGet Vulnerability Audit" "BLOCKED" "Restore failed"}

Invoke-Stage "Python Dependency Audit" {
    Push-Location services\python-analysis
    try{
        python -m pip install --upgrade pip-audit
        if($LASTEXITCODE -ne 0){throw "pip-audit install failed"}
        python -m pip_audit -r requirements.txt
    }finally{Pop-Location}
} "21-python-audit.log" | Out-Null

Invoke-Stage "Python Static Analysis" {
    python -m compileall -q services/python-analysis/app collectors/unreal-collector
} "22-python-static.log" | Out-Null

if(Has-Command cmake){
    Invoke-Stage "C++ Static Build" {
        cmake -S collectors/native-collector -B build/native-security -DBUILD_TESTING=OFF
        if($LASTEXITCODE -ne 0){throw "C++ configure failed"}
        cmake --build build/native-security --config Release --parallel
    } "24-cpp-static-build.log" | Out-Null
}else{Add-Result "C++ Static Build" "BLOCKED" "cmake missing"}

# Gitleaks: local binary if present, otherwise official Docker image.
if(Has-Command gitleaks){
    Invoke-Stage "Gitleaks Secret Scan" {
        gitleaks detect --source . --no-banner --redact
    } "25-gitleaks.log" | Out-Null
}elseif(Has-Command docker){
    Invoke-Stage "Gitleaks Secret Scan" {
        docker info *> $null
        if($LASTEXITCODE -ne 0){throw "Docker Desktop is not running"}
        $mount="type=bind,source=$Repo,target=/repo"
        docker run --rm --mount $mount -w /repo zricethezav/gitleaks:latest `
            detect --source=/repo --no-banner --redact
    } "25-gitleaks.log" | Out-Null
}else{Add-Result "Gitleaks Secret Scan" "BLOCKED" "gitleaks and Docker unavailable"}

# The benchmark intentionally skips unavailable toolchains and never upgrades skips to PASS.
Invoke-Stage "Universal QA Benchmark" {
    python tools/universal_qa_benchmark.py --output artifacts/universal-qa-benchmark
} "26-universal-qa.log" | Out-Null

# PostgreSQL: use existing server or Docker.
$env:POSTGRES_HOST="127.0.0.1"
$env:POSTGRES_PORT="5432"
$env:POSTGRES_DB="dip_test"
$env:POSTGRES_USER="dip"
$env:POSTGRES_PASSWORD="dip-test-password"
$env:DIP_POSTGRES_CONTAINER=$DockerContainer
$env:ConnectionStrings__PostgreSql="Host=127.0.0.1;Port=5432;Database=dip_test;Username=dip;Password=dip-test-password"

$pgReady=Test-TcpPort "127.0.0.1" 5432
if((-not $pgReady) -and (Has-Command docker)){
    docker info *> $null
    if($LASTEXITCODE -eq 0){
        docker rm -f $DockerContainer *> $null
        docker run --name $DockerContainer -e POSTGRES_DB=dip_test -e POSTGRES_USER=dip `
            -e POSTGRES_PASSWORD=dip-test-password -p 5432:5432 -d postgres:16 *> $null
        if($LASTEXITCODE -eq 0){
            $DockerPostgresStarted=$true
            for($i=0;$i -lt 90;$i++){
                if(Test-TcpPort "127.0.0.1" 5432){$pgReady=$true;break}
                Start-Sleep -Seconds 1
            }
        }
    }
}

if($pgReady){Add-Result "PostgreSQL Runtime" "PASS" "Available on 127.0.0.1:5432"}
else{Add-Result "PostgreSQL Runtime" "BLOCKED" "No local PostgreSQL and Docker fallback failed"}

$integration=$false
if($build -and $pgReady){
    $integration=Invoke-Stage "Integration Tests" {
        dotnet test tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj -c Release --no-build
    } "28-integration-tests.log"
}else{Add-Result "Integration Tests" "BLOCKED" "Build/PostgreSQL unavailable"}

if($build -and $pgReady -and $integration){
    Invoke-Stage "API + Worker + E2E" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Verify-SelfHostedWindowsRuntime.ps1
    } "29-runtime-e2e.log" | Out-Null
}else{Add-Result "API + Worker + E2E" "BLOCKED" "Prerequisite unavailable"}

if($build -and $pgReady){
    Invoke-Stage "GUI Runtime + Playwright" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Verify-GuiRuntime.ps1 `
            -RunPlaywright -StartupTimeoutSeconds 900
    } "30-gui-e2e.log" | Out-Null

    Invoke-Stage "Database Migration Verification" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Verify-DatabaseMigrations.ps1
    } "31-database-migrations.log" | Out-Null

    Invoke-Stage "Disaster Recovery Verification" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Verify-DisasterRecovery.ps1
    } "32-disaster-recovery.log" | Out-Null
}else{
    Add-Result "GUI Runtime + Playwright" "BLOCKED" "Build/PostgreSQL unavailable"
    Add-Result "Database Migration Verification" "BLOCKED" "Build/PostgreSQL unavailable"
    Add-Result "Disaster Recovery Verification" "BLOCKED" "Build/PostgreSQL unavailable"
}

if($DockerPostgresStarted){
    docker rm -f $DockerContainer *> $null
}

$Results | Export-Csv (Join-Path $ReportRoot "SUMMARY.csv") -NoTypeInformation -Encoding UTF8
$Results | Format-Table Stage,Status,Detail -AutoSize | Out-String |
    Set-Content (Join-Path $ReportRoot "SUMMARY.txt") -Encoding UTF8
$Results | Format-Table Stage,Status,Detail -AutoSize

$fail=@($Results | Where-Object Status -eq "FAIL")
$blocked=@($Results | Where-Object Status -eq "BLOCKED")
Write-Host "Report: $ReportRoot" -ForegroundColor Cyan
Write-Host "FAIL=$($fail.Count) BLOCKED=$($blocked.Count)" -ForegroundColor Cyan

if($fail.Count -gt 0){exit 1}
if($blocked.Count -gt 0){exit 2}
exit 0
