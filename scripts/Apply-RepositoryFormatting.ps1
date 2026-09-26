# Purpose: Applies repository-wide .NET formatting once, then verifies format, Release build, and core test projects.
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root =
    (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$solution =
    Join-Path `
        $root `
        'DevelopmentIntelligencePlatform.sln'

if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    throw "Solution was not found: $solution"
}

$dotnet =
    (Get-Command dotnet -ErrorAction Stop).Source

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw (
            "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
        )
    }
}

Push-Location $root

try {
    Invoke-DotNet @(
        'restore',
        '.\DevelopmentIntelligencePlatform.sln'
    )

    Invoke-DotNet @(
        'format',
        '.\DevelopmentIntelligencePlatform.sln',
        '--no-restore'
    )

    Invoke-DotNet @(
        'format',
        '.\DevelopmentIntelligencePlatform.sln',
        '--verify-no-changes',
        '--no-restore'
    )

    Invoke-DotNet @(
        'build',
        '.\DevelopmentIntelligencePlatform.sln',
        '-c',
        'Release',
        '--no-restore',
        '--nologo'
    )

    if (-not $SkipTests) {
        foreach ($project in @(
            '.\tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj',
            '.\tests\Dip.Application.Tests\Dip.Application.Tests.csproj',
            '.\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj'
        )) {
            Invoke-DotNet @(
                'test',
                $project,
                '-c',
                'Release',
                '--no-build',
                '--logger',
                'console;verbosity=normal'
            )
        }
    }

    Write-Host (
        'Repository formatting applied and verified successfully.'
    ) -ForegroundColor Green
}
finally {
    Pop-Location
}
