$ErrorActionPreference = "Continue"
Set-StrictMode -Version 2.0

$Repo = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Repo

$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ReportRoot = Join-Path $Repo "artifacts\full-ci-final\$Stamp"
New-Item -ItemType Directory -Force -Path $ReportRoot | Out-Null

$Results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param([string]$Stage,[string]$Status,[string]$Detail,[string]$LogPath="")
    [void]$script:Results.Add([PSCustomObject]@{Stage=$Stage;Status=$Status;Detail=$Detail;Log=$LogPath})
    $color = "Yellow"
    if ($Status -eq "PASS") { $color = "Green" }
    elseif ($Status -eq "FAIL") { $color = "Red" }
    Write-Host "[$Status] $Stage - $Detail" -ForegroundColor $color
}

function Invoke-Stage {
    param([string]$Stage,[scriptblock]$Command,[string]$LogName)
    $LogPath = Join-Path $script:ReportRoot $LogName
    try {
        $global:LASTEXITCODE = 0
        & $Command *>&1 | Tee-Object -FilePath $LogPath
        $code = $LASTEXITCODE
        if ($null -eq $code) { $code = 0 }
        if ($code -eq 0) {
            Add-Result $Stage "PASS" "ExitCode=0" $LogPath
            return $true
        }
        Add-Result $Stage "FAIL" "ExitCode=$code" $LogPath
        return $false
    }
    catch {
        $_ | Out-String | Add-Content -Path $LogPath -Encoding UTF8
        Add-Result $Stage "FAIL" $_.Exception.Message $LogPath
        return $false
    }
}

function Has-Command { param([string]$Name) return ($null -ne (Get-Command $Name -ErrorAction SilentlyContinue)) }

$Sha=(git rev-parse HEAD).Trim()
Add-Result "Git working tree" $(if (@(git status --porcelain).Count -eq 0){"PASS"}else{"WARN"}) "SHA=$Sha"

$required=@(
"README.md","DevelopmentIntelligencePlatform.sln",
"tools\verify_publication_safety.py","tools\validate_portfolio_readiness.py",
"scripts\Verify-SelfHostedWindowsRuntime.ps1","scripts\Verify-GuiRuntime.ps1"
)
$missing=@($required | Where-Object { -not (Test-Path $_) })
if ($missing.Count -eq 0) { Add-Result "Required files" "PASS" "All present" }
else { Add-Result "Required files" "FAIL" ($missing -join ", ") }

Invoke-Stage "Publication Safety" {
    python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
} "04-publication-safety.log" | Out-Null

Invoke-Stage "Portfolio Static Readiness" {
    python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
} "05-portfolio-readiness.log" | Out-Null

$empty=@(Get-ChildItem src\Dip.Web -Recurse -Filter *.razor -File -ErrorAction SilentlyContinue | Where-Object Length -eq 0)
if ($empty.Count -eq 0) { Add-Result "Empty Razor Files" "PASS" "0 files" }
else { Add-Result "Empty Razor Files" "FAIL" "$($empty.Count) empty files" }

$restore=Invoke-Stage ".NET Restore" { dotnet restore DevelopmentIntelligencePlatform.sln } "07-dotnet-restore.log"
$build=$false
if ($restore) { $build=Invoke-Stage ".NET Release Build" { dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore } "08-dotnet-build.log" }
else { Add-Result ".NET Release Build" "BLOCKED" "Restore failed" }

if ($build) {
    Invoke-Stage "Domain Tests" { dotnet test tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj -c Release --no-build } "09-domain-tests.log" | Out-Null
    Invoke-Stage "Application Tests" { dotnet test tests/Dip.Application.Tests/Dip.Application.Tests.csproj -c Release --no-build } "10-application-tests.log" | Out-Null
    Invoke-Stage "Infrastructure Tests" { dotnet test tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj -c Release --no-build } "11-infrastructure-tests.log" | Out-Null
} else {
    Add-Result "Domain Tests" "BLOCKED" "Build failed"
    Add-Result "Application Tests" "BLOCKED" "Build failed"
    Add-Result "Infrastructure Tests" "BLOCKED" "Build failed"
}

Invoke-Stage "Python Pytest" {
    Push-Location services\python-analysis
    try { python -m pip install -e ".[dev]"; if ($LASTEXITCODE -ne 0) { throw "Python install failed" }; python -m pytest }
    finally { Pop-Location }
} "14-python-tests.log" | Out-Null

Invoke-Stage "Unreal Collector Tests" {
    python -m compileall collectors/unreal-collector
    if ($LASTEXITCODE -ne 0) { throw "compileall failed" }
    python -m unittest discover -s collectors/unreal-collector/tests -v
} "16-unreal-tests.log" | Out-Null

if ((Has-Command cmake) -and (Has-Command ctest)) {
    Invoke-Stage "Native Collector" {
        cmake -S collectors/native-collector -B build/native-local-ci -DBUILD_TESTING=ON
        if ($LASTEXITCODE -ne 0) { throw "configure failed" }
        cmake --build build/native-local-ci --config Release --parallel
        if ($LASTEXITCODE -ne 0) { throw "build failed" }
        ctest --test-dir build/native-local-ci -C Release --output-on-failure
    } "19-native-tests.log" | Out-Null
} else { Add-Result "Native Collector" "BLOCKED" "cmake/ctest missing" }

if ($restore) {
    Invoke-Stage "NuGet Vulnerability Audit" {
        $Output = dotnet list DevelopmentIntelligencePlatform.sln package --vulnerable --include-transitive 2>&1
        $Code = $LASTEXITCODE
        $Output | Write-Output
        if ($Code -ne 0) { throw "dotnet list package failed" }
        if (($Output | Out-String) -match "has the following vulnerable packages") { throw "Vulnerable NuGet packages detected" }
        $global:LASTEXITCODE = 0
    } "20-nuget-audit.log" | Out-Null
}

Invoke-Stage "Python Dependency Audit" {
    Push-Location services\python-analysis
    try {
        python -m pip install --upgrade pip-audit
        if ($LASTEXITCODE -ne 0) { throw "pip-audit install failed" }
        python -m pip_audit -r requirements.txt
    } finally { Pop-Location }
} "21-python-audit.log" | Out-Null

if (Has-Command gitleaks) {
    Invoke-Stage "Gitleaks" { gitleaks detect --source . --no-banner --redact } "25-gitleaks.log" | Out-Null
} else { Add-Result "Gitleaks" "BLOCKED" "gitleaks not installed" }

$missingQa=@("node","npm","go","java","javac","tsc" | Where-Object { -not (Has-Command $_) })
if ($missingQa.Count -eq 0) {
    Invoke-Stage "Universal QA" { python tools/universal_qa_benchmark.py --output artifacts/universal-qa-benchmark } "26-universal-qa.log" | Out-Null
} else { Add-Result "Universal QA" "BLOCKED" ("Missing: "+($missingQa -join ", ")) }

$pgReady=$false
$containerStarted=$false
$container="dip-local-ci-postgres"
$env:POSTGRES_HOST="127.0.0.1"; $env:POSTGRES_PORT="5432"; $env:POSTGRES_DB="dip_test"; $env:POSTGRES_USER="dip"; $env:POSTGRES_PASSWORD="dip-test-password"
$env:ConnectionStrings__PostgreSql="Host=127.0.0.1;Port=5432;Database=dip_test;Username=dip;Password=dip-test-password"
$env:ASPNETCORE_ENVIRONMENT="Testing"; $env:DOTNET_ENVIRONMENT="Testing"; $env:ASPNETCORE_URLS="http://127.0.0.1:7001"
$env:Authentication__RequireAuthenticatedUser="false"; $env:GitHub__WebhookSecret="integration-test-secret"; $env:GitHub__Token="integration-test-token"
$env:DIP_API_URL="http://127.0.0.1:7001"; $env:Qa__WorkspaceRoot=$Repo; $env:Qa__ArtifactRoot=Join-Path $Repo "artifacts\qa"
$env:Artifacts__Provider="Local"; $env:Artifacts__LocalRoot=Join-Path $Repo "artifacts\durable"

if (Has-Command pg_isready) {
    pg_isready -h 127.0.0.1 -p 5432 *> $null
    if ($LASTEXITCODE -eq 0) { $pgReady=$true }
}
if ((-not $pgReady) -and (Has-Command docker)) {
    docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        docker rm -f $container *> $null
        docker run --name $container -e POSTGRES_DB=dip_test -e POSTGRES_USER=dip -e POSTGRES_PASSWORD=dip-test-password -p 5432:5432 -d postgres:16 *> $null
        if ($LASTEXITCODE -eq 0) {
            $containerStarted=$true
            for($i=0;$i -lt 60;$i++){
                docker exec $container pg_isready -U dip -d dip_test *> $null
                if($LASTEXITCODE -eq 0){$pgReady=$true;break}
                Start-Sleep 1
            }
        }
    }
}
if($pgReady){Add-Result "PostgreSQL Runtime" "PASS" "Available"}else{Add-Result "PostgreSQL Runtime" "BLOCKED" "Unavailable"}

if($build -and $pgReady){
    $integration=Invoke-Stage "Integration Tests" { dotnet test tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj -c Release --no-build } "28-integration-tests.log"
    if($integration){
        Invoke-Stage "API + Worker + E2E" {
            powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Verify-SelfHostedWindowsRuntime.ps1
        } "29-runtime-e2e.log" | Out-Null
    } else { Add-Result "API + Worker + E2E" "BLOCKED" "Integration failed" }
} else {
    Add-Result "Integration Tests" "BLOCKED" "Build/PostgreSQL unavailable"
    Add-Result "API + Worker + E2E" "BLOCKED" "Prerequisite unavailable"
}

if($build -and $pgReady -and (Has-Command node) -and (Has-Command npm)){
    Invoke-Stage "GUI Runtime + Playwright" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Verify-GuiRuntime.ps1 -RunPlaywright -StartupTimeoutSeconds 900
    } "30-gui-e2e.log" | Out-Null
} else { Add-Result "GUI Runtime + Playwright" "BLOCKED" "Build/PostgreSQL/Node/npm required" }

if($containerStarted){ docker rm -f $container *> $null }

$Results | Export-Csv (Join-Path $ReportRoot "SUMMARY.csv") -NoTypeInformation -Encoding UTF8
$Results | Format-Table Stage,Status,Detail -AutoSize | Out-String | Set-Content (Join-Path $ReportRoot "SUMMARY.txt") -Encoding UTF8
$Results | Format-Table Stage,Status,Detail -AutoSize
Write-Host "Report: $ReportRoot" -ForegroundColor Cyan
