# Purpose: Resolves local or Docker-hosted PostgreSQL client tooling for verification scripts without requiring a local PostgreSQL installation.

function Resolve-DipPostgresContainer {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [int]$HostPort,

        [string]$PreferredName = ''
    )

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker is required when local PostgreSQL client tools are unavailable.'
    }

    & docker info --format '{{.ServerVersion}}' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Desktop is not running.'
    }

    $rows = @(& docker ps --format '{{.Names}}|{{.Image}}|{{.Ports}}')
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate running Docker containers.'
    }

    if (-not [string]::IsNullOrWhiteSpace($PreferredName)) {
        $preferredRows = @(
            $rows |
                Where-Object {
                    ($_ -split '\|', 3)[0] -eq $PreferredName
                }
        )

        if ($preferredRows.Count -eq 1) {
            return $PreferredName
        }

        throw "Configured PostgreSQL container '$PreferredName' is not running."
    }

    $needle = ([string]$HostPort) + '->5432/tcp'
    $matches = @()

    foreach ($row in $rows) {
        if ([string]::IsNullOrWhiteSpace($row)) {
            continue
        }

        $parts = $row -split '\|', 3
        if ($parts.Count -lt 3) {
            continue
        }

        $name = $parts[0]
        $image = $parts[1]
        $ports = $parts[2]

        if ($image -notmatch '^postgres(?::|$)') {
            continue
        }

        if ($ports.Contains($needle)) {
            $matches += $name
        }
    }

    if ($matches.Count -eq 1) {
        return $matches[0]
    }

    if ($matches.Count -gt 1) {
        throw (
            "Multiple PostgreSQL containers expose host port $HostPort. " +
            "Set DIP_POSTGRES_CONTAINER explicitly. Candidates: " +
            ($matches -join ', ')
        )
    }

    throw (
        "No running PostgreSQL Docker container exposes host port $HostPort. " +
        "Set DIP_POSTGRES_CONTAINER when the container cannot be discovered by port."
    )
}

function Resolve-DipPostgresClient {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$HostName,

        [Parameter(Mandatory = $true)]
        [int]$Port,

        [Parameter(Mandatory = $true)]
        [string]$User,

        [Parameter(Mandatory = $true)]
        [string]$Password,

        [switch]$RequireBackupTools
    )

    $hasPsql = [bool](Get-Command psql -ErrorAction SilentlyContinue)
    $hasPgDump = [bool](Get-Command pg_dump -ErrorAction SilentlyContinue)
    $hasPgRestore = [bool](Get-Command pg_restore -ErrorAction SilentlyContinue)

    $localReady = $hasPsql
    if ($RequireBackupTools) {
        $localReady = $hasPsql -and $hasPgDump -and $hasPgRestore
    }

    if ($localReady) {
        return [pscustomobject]@{
            Mode = 'Local'
            Container = ''
            Host = $HostName
            Port = $Port
            User = $User
            Password = $Password
        }
    }

    $preferred = if ($env:DIP_POSTGRES_CONTAINER) {
        $env:DIP_POSTGRES_CONTAINER
    }
    else {
        ''
    }

    $container = Resolve-DipPostgresContainer `
        -HostPort $Port `
        -PreferredName $preferred

    return [pscustomobject]@{
        Mode = 'Docker'
        Container = $container
        Host = $Host
        Port = $Port
        User = $User
        Password = $Password
    }
}

function Invoke-DipPsql {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Client,

        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$Sql,

        [switch]$TuplesOnly,

        [switch]$DiscardOutput
    )

    if ($Client.Mode -eq 'Local') {
        $oldPassword = $env:PGPASSWORD
        $env:PGPASSWORD = $Client.Password

        try {
            $arguments = @(
                '-h', $Client.Host,
                '-p', [string]$Client.Port,
                '-U', $Client.User,
                '-d', $Database,
                '-v', 'ON_ERROR_STOP=1',
                '-w'
            )

            if ($TuplesOnly) {
                $arguments += @('-A', '-t')
            }

            $arguments += @('-c', $Sql)

            if ($DiscardOutput) {
                & psql @arguments *> $null
                $output = $null
            }
            else {
                $output = & psql @arguments
            }

            if ($LASTEXITCODE -ne 0) {
                throw "psql failed for database '$Database'."
            }

            return $output
        }
        finally {
            $env:PGPASSWORD = $oldPassword
        }
    }

    $arguments = @(
        'exec',
        '-e', "PGPASSWORD=$($Client.Password)",
        $Client.Container,
        'psql',
        '-U', $Client.User,
        '-d', $Database,
        '-v', 'ON_ERROR_STOP=1',
        '-w'
    )

    if ($TuplesOnly) {
        $arguments += @('-A', '-t')
    }

    $arguments += @('-c', $Sql)

    if ($DiscardOutput) {
        & docker @arguments *> $null
        $output = $null
    }
    else {
        $output = & docker @arguments
    }

    if ($LASTEXITCODE -ne 0) {
        throw "docker psql failed for database '$Database' in container '$($Client.Container)'."
    }

    return $output
}

function Test-DipPostgresReady {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Client
    )

    try {
        $probe = (
            Invoke-DipPsql `
                -Client $Client `
                -Database 'postgres' `
                -Sql 'SELECT 1;' `
                -TuplesOnly |
            Out-String
        ).Trim()

        return $probe -eq '1'
    }
    catch {
        return $false
    }
}

function Get-DipPostgresRuntimeLabel {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Client
    )

    if ($Client.Mode -eq 'Local') {
        return 'native-postgresql-client'
    }

    return 'docker-postgresql-client'
}
