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

function Wait-Http($name,$url,$deadline){
    do{
        try{
            $r=Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 5
            if($r.StatusCode -ge 200 -and $r.StatusCode -lt 400){
                Write-Host "$name PASS" -ForegroundColor Green
                return $r
            }
        }catch{}
        Start-Sleep -Seconds 1
    }while((Get-Date) -lt $deadline)
    throw "$name timeout: $url"
}

try{
    & (Join-Path $PSScriptRoot 'Start-DIP.ps1') -NoBrowser -HealthTimeoutSeconds $StartupTimeoutSeconds
    $started=$true
    $deadline=(Get-Date).AddSeconds($StartupTimeoutSeconds)

    $web='http://127.0.0.1:5081'
    $api='http://127.0.0.1:5080'

    $home=Wait-Http 'Web GUI' "$web/" $deadline
    if($home.Content -notmatch 'Development Intelligence'){throw 'DIP application shell was not rendered.'}

    $status=Invoke-RestMethod "$api/api/v1/system/status"
    foreach($key in 'api','database','analysis','worker','artifact-store'){
        $c=@($status.components | Where-Object{$_.key -eq $key}) | Select-Object -First 1
        if(-not $c -or $c.state -ne 'Healthy'){throw "System component '$key' is not healthy."}
    }

    $name='GUI Runtime Smoke '+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $body=@{name=$name;repositoryUrl='https://github.com/example/dip-gui-runtime-smoke';defaultBranch='main';releaseDateUtc=$null}|ConvertTo-Json
    $created=Invoke-RestMethod "$api/api/projects" -Method Post -ContentType 'application/json' -Body $body
    if(-not $created.id){throw 'Project creation did not return an id.'}
    $projectId=[string]$created.id

    foreach($path in @(
        "dashboard/$projectId",
        "dashboard/$projectId/qa",
        "dashboard/$projectId/management",
        "dashboard/$projectId/intelligence",
        "dashboard/$projectId/qa-governance"
    )){
        Wait-Http $path "$web/$path" $deadline | Out-Null
    }

    $playwrightPassed=$false
    if($RunPlaywright){
        if(Get-Command npm -ErrorAction SilentlyContinue){
            Push-Location (Join-Path $root 'tests\Dip.UiE2E')
            try{
                if(Test-Path package-lock.json){ npm ci --no-audit --no-fund }else{ npm install --no-audit --no-fund }
                if($LASTEXITCODE -ne 0){throw 'npm install failed'}
                npx playwright install chromium
                if($LASTEXITCODE -ne 0){throw 'Playwright browser install failed'}
                $env:DIP_WEB_BASE_URL=$web
                $env:DIP_API_BASE_URL=$api
                $args=@('playwright','test','--grep-invert','@live-github')
                if($Headed){$args+='--headed'}
                npx @args
                if($LASTEXITCODE -ne 0){throw 'Playwright E2E failed'}
                $playwrightPassed=$true
            }
            finally{Pop-Location}
        }
        elseif(Get-Command docker -ErrorAction SilentlyContinue){
            docker info *> $null
            if($LASTEXITCODE -ne 0){throw 'npm is missing and Docker Desktop is not running.'}

            $mount="type=bind,source=$root,target=/work"
            docker run --rm `
                --add-host host.docker.internal:host-gateway `
                --mount $mount `
                -w /work/tests/Dip.UiE2E `
                -e DIP_WEB_BASE_URL=http://host.docker.internal:5081 `
                -e DIP_API_BASE_URL=http://host.docker.internal:5080 `
                mcr.microsoft.com/playwright:v1.55.0-noble `
                bash -lc "npm ci --no-audit --no-fund && npx playwright test --grep-invert '@live-github'"

            if($LASTEXITCODE -ne 0){throw 'Docker Playwright E2E failed'}
            $playwrightPassed=$true
        }
        else{
            throw 'Playwright requires npm or Docker.'
        }
    }

    $sha=(& git -C $root rev-parse HEAD | Out-String).Trim()
    [ordered]@{
        schema='dip-gui-runtime-verification-v4'
        runtime='native'
        sourceCommit=$sha
        verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
        projectId=$projectId
        systemStatus='PASS'
        playwright=if($RunPlaywright){if($playwrightPassed){'PASS'}else{'FAIL'}}else{'NOT_REQUESTED'}
    } | ConvertTo-Json | Set-Content (Join-Path $out 'gui-runtime-verification.json') -Encoding UTF8

    "GUI RUNTIME VERIFICATION PASS`nRuntime=Native`nCommit=$sha" |
        Set-Content (Join-Path $out 'GUI_RUNTIME_VERIFICATION_PASS.txt') -Encoding UTF8
    $success=$true
}
finally{
    if($started -and (-not $KeepOnSuccess -or -not $success) -and (-not $KeepOnFailure -or $success)){
        & (Join-Path $PSScriptRoot 'Stop-DIP.ps1') -KeepData
    }
}
