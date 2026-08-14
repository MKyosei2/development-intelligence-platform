[CmdletBinding()]
param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [switch]$DotNetOnly,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts\runtime-verification' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$fullPass = Join-Path $OutputDirectory 'RUNTIME_VERIFICATION_PASS.json'
$partial = Join-Path $OutputDirectory 'WINDOWS_DOTNET_RUNTIME_PASS.json'
Remove-Item -LiteralPath $fullPass,$partial -Force -ErrorAction SilentlyContinue

if (-not $IsWindows) { throw 'Formal runtime verification must run on Windows.' }
$dotnet = Get-Command dotnet -ErrorAction Stop
$git = Get-Command git -ErrorAction Stop
$commit = (& $git.Source -C $root rev-parse HEAD).Trim()
$dotnetVersion = (& $dotnet.Source --version).Trim()
$started = [DateTimeOffset]::UtcNow

$steps = [System.Collections.Generic.List[object]]::new()
function Invoke-VerifiedStep {
    param([string]$Name,[scriptblock]$Action)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
        $sw.Stop()
        $steps.Add([ordered]@{ name=$Name; status='PASS'; durationMs=$sw.ElapsedMilliseconds })
    }
    catch {
        $sw.Stop()
        $steps.Add([ordered]@{ name=$Name; status='FAIL'; durationMs=$sw.ElapsedMilliseconds; error=$_.Exception.Message })
        throw
    }
}

Push-Location $root
try {
    Invoke-VerifiedStep 'dotnet-restore' { dotnet restore .\DevelopmentIntelligencePlatform.sln --nologo }
    Invoke-VerifiedStep 'dotnet-build-release' { dotnet build .\DevelopmentIntelligencePlatform.sln -c Release --no-restore --nologo -v minimal }
    Invoke-VerifiedStep 'dotnet-test-release' { dotnet test .\DevelopmentIntelligencePlatform.sln -c Release --no-build --nologo --logger 'trx;LogFileName=runtime-verification.trx' --results-directory $OutputDirectory }

    $unity = [ordered]@{ required=(-not $DotNetOnly); status=if($DotNetOnly){'NOT_REQUESTED'}else{'PENDING'}; version=$null; executable=$null }
    if (-not $DotNetOnly) {
        if (-not $UnityPath) { throw 'UNITY_EDITOR_PATH/-UnityPath is required for a full runtime PASS.' }
        $resolvedUnity = (Resolve-Path -LiteralPath $UnityPath -ErrorAction Stop).Path
        $unity.executable = $resolvedUnity
        $versionOutput = (& $resolvedUnity -version 2>&1 | Out-String).Trim()
        $unity.version = $versionOutput
        Invoke-VerifiedStep 'unity-collector-runtime' { & (Join-Path $PSScriptRoot 'Verify-UnityCollector.ps1') -UnityPath $resolvedUnity }
        $unity.status = 'PASS'
    }

    $payload = [ordered]@{
        schemaVersion = '1.0'
        status = if($DotNetOnly){'WINDOWS_DOTNET_PASS'}else{'PASS'}
        commit = $commit
        startedAtUtc = $started.ToString('O')
        finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        machine = [ordered]@{
            os = [Environment]::OSVersion.VersionString
            architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            framework = [Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
            dotnetSdk = $dotnetVersion
            computerName = $env:COMPUTERNAME
            githubRunner = $env:RUNNER_NAME
        }
        windowsDotNet = [ordered]@{ status='PASS'; configuration='Release' }
        unity = $unity
        steps = $steps
    }
    $path = if($DotNetOnly){$partial}else{$fullPass}
    $payload | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding UTF8
    Write-Host "Runtime evidence generated: $path" -ForegroundColor Green
}
finally { Pop-Location }
