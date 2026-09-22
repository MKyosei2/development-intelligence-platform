# Purpose: Verifies G ui Ru nt im e requirements for DIP.
[CmdletBinding()]
param(
    [int]$StartupTimeoutSeconds=420,
    [switch]$RunPlaywright,
    [switch]$Headed,
    [switch]$ReuseImages,
    [switch]$KeepOnFailure,
    [switch]$KeepOnSuccess
)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stamp=[DateTimeOffset]::UtcNow.ToString('yyyyMMdd_HHmmss')
$out=Join-Path $root "artifacts\gui-runtime-verification\$stamp"
New-Item -Force -ItemType Directory $out | Out-Null
$started=$false
$success=$false
$currentPhase='INITIALIZATION'
$phaseLog=Join-Path $out 'phase-timeline.log'

# Windows PowerShell resolves npm/npx to npm.ps1/npx.ps1 before the .cmd shims,
# which can be blocked by ExecutionPolicy. Use the .cmd shims explicitly on Windows.
$npmCommand=if($env:OS -eq 'Windows_NT'){'npm.cmd'}else{'npm'}
$npxCommand=if($env:OS -eq 'Windows_NT'){'npx.cmd'}else{'npx'}
$dockerCommand=if($env:OS -eq 'Windows_NT'){'docker.exe'}else{'docker'}

function Start-Phase([string]$name){
    $script:currentPhase=$name
    $line="[PHASE START] $([DateTimeOffset]::UtcNow.ToString('o')) | $name"
    Write-Host $line -ForegroundColor Cyan
    Add-Content -LiteralPath $phaseLog -Value $line
}

function Pass-Phase([string]$name){
    $line="[PHASE PASS] $([DateTimeOffset]::UtcNow.ToString('o')) | $name"
    Write-Host $line -ForegroundColor Green
    Add-Content -LiteralPath $phaseLog -Value $line
}

function Save-Json([string]$name,$value){
    $value | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $out $name) -Encoding UTF8
}

function Wait-Http($name,$url,$deadline){
    $lastError=$null
    do{
        try{
            $r=Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 5
            if($r.StatusCode -ge 200 -and $r.StatusCode -lt 400){
                Write-Host "$name PASS" -ForegroundColor Green
                return $r
            }
            $lastError="HTTP $($r.StatusCode)"
        }
        catch{
            $lastError=$_.Exception.Message
        }
        Start-Sleep -Seconds 1
    }while((Get-Date) -lt $deadline)
    throw "$name timeout: $url. Last error: $lastError"
}

function Wait-SystemStatusHealthy([string]$api,[datetime]$deadline){
    $attempt=0
    $lastDetail='No status response was received.'
    do{
        $attempt++
        try{
            $status=Invoke-RestMethod -Uri "$api/api/v1/system/status" -Method Get -TimeoutSec 15
            Save-Json ("system-status-attempt-{0:D2}.json" -f $attempt) $status

            $bad=[System.Collections.Generic.List[string]]::new()
            foreach($key in @('api','database','analysis','worker','artifact-store')){
                $component=@($status.components | Where-Object{$_.key -eq $key}) | Select-Object -First 1
                if($null -eq $component){
                    $bad.Add("$key=missing") | Out-Null
                }
                elseif([string]$component.state -ne 'Healthy'){
                    $bad.Add("$key=$([string]$component.state)") | Out-Null
                }
            }

            if($bad.Count -eq 0){
                return $status
            }

            $lastDetail=($bad -join ', ')
            Write-Warning "System status is not fully healthy yet: $lastDetail"
        }
        catch{
            $lastDetail=$_.Exception.Message
            [ordered]@{
                attempt=$attempt
                exceptionType=$_.Exception.GetType().FullName
                message=$_.Exception.Message
                scriptStackTrace=$_.ScriptStackTrace
            } | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $out ("system-status-attempt-{0:D2}-error.json" -f $attempt)) -Encoding UTF8
        }

        Start-Sleep -Seconds 1
    }while((Get-Date) -lt $deadline)

    throw "System status did not become healthy before the deadline. Last detail: $lastDetail"
}

try{
    # GUI E2E must exercise project creation. Pin the native verification runtime
    # to writable development mode instead of inheriting a portfolio/read-only
    # environment variable from the host.
    $env:PublicAccess__ReadOnly='false'

    Start-Phase 'START_DIP'
    # GitHub-hosted pwsh can crash during teardown when Start-Process owns
    # RedirectStandardOutput/Error async readers. For GUI E2E only, inherit the
    # runner console instead; normal Start-DIP callers keep file redirection.
    & (Join-Path $PSScriptRoot 'Start-DIP.ps1') -NoBrowser -NoLogRedirect -HealthTimeoutSeconds $StartupTimeoutSeconds
    $started=$true
    Pass-Phase 'START_DIP'

    $deadline=(Get-Date).AddSeconds($StartupTimeoutSeconds)
    $web='http://127.0.0.1:5081'
    $api='http://127.0.0.1:5080'

    Start-Phase 'WEB_GUI'
    # PowerShell variable names are case-insensitive. $HOME is a read-only
    # automatic variable on Linux/macOS, so never assign the Web response to $home.
    $homeResponse=Wait-Http 'Web GUI' "$web/" $deadline
    if($homeResponse.Content -notmatch 'Development Intelligence'){throw 'DIP application shell was not rendered.'}
    Pass-Phase 'WEB_GUI'

    Start-Phase 'SYSTEM_STATUS'
    $status=Wait-SystemStatusHealthy $api $deadline
    Save-Json 'system-status-final.json' $status
    Pass-Phase 'SYSTEM_STATUS'

    Start-Phase 'PROJECT_CREATION'
    $name='GUI Runtime Smoke '+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $request=[ordered]@{
        name=$name
        repositoryUrl='https://github.com/example/dip-gui-runtime-smoke'
        defaultBranch='main'
        releaseDateUtc=$null
    }
    Save-Json 'project-create-request.json' $request
    $body=$request | ConvertTo-Json

    try{
        $created=Invoke-RestMethod "$api/api/projects" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 30
    }
    catch{
        [ordered]@{
            phase='PROJECT_CREATION'
            exceptionType=$_.Exception.GetType().FullName
            message=$_.Exception.Message
            scriptStackTrace=$_.ScriptStackTrace
            fullError=($_ | Out-String)
        } | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $out 'project-create-error.json') -Encoding UTF8
        throw
    }

    Save-Json 'project-create-response.json' $created
    if(-not $created.id){throw 'Project creation did not return an id.'}
    $projectId=[string]$created.id
    Pass-Phase 'PROJECT_CREATION'

    foreach($path in @(
        "dashboard/$projectId",
        "dashboard/$projectId/qa",
        "dashboard/$projectId/management",
        "dashboard/$projectId/intelligence",
        "dashboard/$projectId/qa-governance"
    )){
        $pagePhase='PAGE_'+($path -replace '[^A-Za-z0-9]+','_').Trim('_').ToUpperInvariant()
        Start-Phase $pagePhase
        Wait-Http $path "$web/$path" $deadline | Out-Null
        Pass-Phase $pagePhase
    }

    $playwrightPassed=$false
    if($RunPlaywright){
        if(Get-Command $npmCommand -ErrorAction SilentlyContinue){
            $uiE2e=Join-Path $root 'tests/Dip.UiE2E'
            Push-Location $uiE2e
            try{
                Start-Phase 'NPM_DEPENDENCIES'
                if(Test-Path package-lock.json){ & $npmCommand ci --no-audit --no-fund }else{ & $npmCommand install --no-audit --no-fund }
                if($LASTEXITCODE -ne 0){throw "npm install failed with exit code $LASTEXITCODE"}
                Pass-Phase 'NPM_DEPENDENCIES'

                Start-Phase 'PLAYWRIGHT_BROWSER_INSTALL'
                & $npxCommand playwright install chromium
                if($LASTEXITCODE -ne 0){throw "Playwright browser install failed with exit code $LASTEXITCODE"}
                Pass-Phase 'PLAYWRIGHT_BROWSER_INSTALL'

                Start-Phase 'PLAYWRIGHT_TESTS'
                $env:DIP_WEB_BASE_URL=$web
                $env:DIP_API_BASE_URL=$api
                # Native GUI verification is intentionally writable. Run only the
                # native onboarding suite here; hosted/public read-only coverage is
                # executed by the deployment verification path instead.
                # A deterministic native failure should fail fast rather than spend
                # another full timeout window on the CI retry configured globally.
                $playwrightArgs=@(
                    'playwright',
                    'test',
                    'tests/gui-onboarding.spec.mjs',
                    '--retries=0'
                )
                if($Headed){$playwrightArgs+='--headed'}
                & $npxCommand @playwrightArgs
                if($LASTEXITCODE -ne 0){throw "Playwright E2E failed with exit code $LASTEXITCODE"}
                $playwrightPassed=$true
                Pass-Phase 'PLAYWRIGHT_TESTS'
            }
            finally{Pop-Location}
        }
        elseif(Get-Command $dockerCommand -ErrorAction SilentlyContinue){
            Start-Phase 'PLAYWRIGHT_DOCKER_TESTS'
            & $dockerCommand info *> $null
            if($LASTEXITCODE -ne 0){throw 'npm is missing and Docker Desktop is not running.'}

            $mount="type=bind,source=$root,target=/work"
            & $dockerCommand run --rm `
                --add-host host.docker.internal:host-gateway `
                --mount $mount `
                -w /work/tests/Dip.UiE2E `
                -e DIP_WEB_BASE_URL=http://host.docker.internal:5081 `
                -e DIP_API_BASE_URL=http://host.docker.internal:5080 `
                mcr.microsoft.com/playwright:v1.55.0-noble `
                bash -lc "if [ -f package-lock.json ]; then npm ci --no-audit --no-fund; else npm install --package-lock=false --no-audit --no-fund; fi && npx playwright test tests/gui-onboarding.spec.mjs --retries=0"

            if($LASTEXITCODE -ne 0){throw "Docker Playwright E2E failed with exit code $LASTEXITCODE"}
            $playwrightPassed=$true
            Pass-Phase 'PLAYWRIGHT_DOCKER_TESTS'
        }
        else{
            throw 'Playwright requires npm or Docker.'
        }
    }

    Start-Phase 'WRITE_EVIDENCE'
    $sha=(& git -C $root rev-parse HEAD | Out-String).Trim()
    [ordered]@{
        schema='dip-gui-runtime-verification-v5'
        runtime='native'
        sourceCommit=$sha
        verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
        projectId=$projectId
        systemStatus='PASS'
        playwright=if($RunPlaywright){if($playwrightPassed){'PASS'}else{'FAIL'}}else{'NOT_REQUESTED'}
        processLogRedirection='DISABLED_FOR_GUI_E2E'
    } | ConvertTo-Json | Set-Content (Join-Path $out 'gui-runtime-verification.json') -Encoding UTF8

    "GUI RUNTIME VERIFICATION PASS`nRuntime=Native`nCommit=$sha" |
        Set-Content (Join-Path $out 'GUI_RUNTIME_VERIFICATION_PASS.txt') -Encoding UTF8
    Pass-Phase 'WRITE_EVIDENCE'
    $success=$true
}
catch{
    # Persist the FIRST functional exception before cleanup. This prevents any
    # secondary process-teardown failure from hiding the actual failing phase.
    [ordered]@{
        phase=$currentPhase
        exceptionType=$_.Exception.GetType().FullName
        message=$_.Exception.Message
        scriptStackTrace=$_.ScriptStackTrace
        stackTrace=$_.Exception.StackTrace
        fullError=($_ | Out-String)
        capturedAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
    } | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $out 'FIRST_FUNCTIONAL_EXCEPTION.json') -Encoding UTF8

    Write-Host "[FIRST FAILURE] Phase=$currentPhase Type=$($_.Exception.GetType().FullName) Message=$($_.Exception.Message)" -ForegroundColor Red
    throw
}
finally{
    if($started -and (-not $KeepOnSuccess -or -not $success) -and (-not $KeepOnFailure -or $success)){
        $cleanup=[ordered]@{
            attempted=$true
            success=$false
            message=''
            completedAtUtc=$null
        }

        try{
            & (Join-Path $PSScriptRoot 'Stop-DIP.ps1') -KeepData
            $cleanup.success=$true
            $cleanup.message='DIP processes stopped.'
        }
        catch{
            # Cleanup diagnostics are recorded, but a cleanup exception must not
            # replace an already captured functional exception.
            $cleanup.message=$_.Exception.Message
            Write-Warning ("DIP cleanup failed after verification: " + $_.Exception.Message)
        }
        finally{
            $cleanup.completedAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
            try{
                $cleanup | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'cleanup-status.json') -Encoding UTF8
            }
            catch{
                Write-Warning ("Could not write cleanup diagnostics: " + $_.Exception.Message)
            }
        }
    }
}
