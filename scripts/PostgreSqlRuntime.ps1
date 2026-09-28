# Purpose: Resolves local or Docker-hosted PostgreSQL client tooling for verification scripts without requiring a local PostgreSQL installation.

function Invoke-DipNativeCapture {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$File,

        [string[]]$Arguments = @()
    )

    # Windows PowerShell 5.1 can promote native-process stderr records when the
    # caller uses $ErrorActionPreference='Stop'. Native tools such as Docker,
    # Git, psql and gitleaks legitimately write progress/information to stderr.
    # Capture both streams under Continue and decide success strictly by exit code.
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try {
        $lines = @(
            & $File @Arguments 2>&1 |
                ForEach-Object { $_.ToString() }
        )

        $exitCode = $LASTEXITCODE
        if ($null -eq $exitCode) {
            $exitCode = 0
        }
    }
    catch {
        $lines = @($_ | Out-String)
        $exitCode = -999
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }

    return [pscustomobject]@{
        ExitCode = [int]$exitCode
        Lines = @($lines)
        Text = ($lines -join [Environment]::NewLine)
    }
}

function Invoke-DipNativeChecked {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$File,

        [string[]]$Arguments = @(),

        [switch]$DiscardOutput,

        [string]$FailureMessage = ''
    )

    $result = Invoke-DipNativeCapture -File $File -Arguments $Arguments

    if ($result.ExitCode -ne 0) {
        $message = if ([string]::IsNullOrWhiteSpace($FailureMessage)) {
            "$File $($Arguments -join ' ') failed with exit code $($result.ExitCode)."
        }
        else {
            $FailureMessage
        }

        if (-not [string]::IsNullOrWhiteSpace($result.Text)) {
            $message += "`n$($result.Text)"
        }

        throw $message
    }

    if (-not $DiscardOutput) {
        return @($result.Lines)
    }
}

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

    Invoke-DipNativeChecked `
        -File 'docker' `
        -Arguments @('info','--format','{{.ServerVersion}}') `
        -DiscardOutput `
        -FailureMessage 'Docker Desktop is not running.'

    $rows = @(
        Invoke-DipNativeChecked `
            -File 'docker' `
            -Arguments @('ps','--format','{{.Names}}|{{.Image}}|{{.Ports}}') `
            -FailureMessage 'Could not enumerate running Docker containers.'
    )

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
        Host = $HostName
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

            $output = Invoke-DipNativeChecked `
                -File 'psql' `
                -Arguments $arguments `
                -DiscardOutput:$DiscardOutput `
                -FailureMessage "psql failed for database '$Database'."

            if (-not $DiscardOutput) {
                return @($output)
            }
            return
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

    $output = Invoke-DipNativeChecked `
        -File 'docker' `
        -Arguments $arguments `
        -DiscardOutput:$DiscardOutput `
        -FailureMessage "docker psql failed for database '$Database' in container '$($Client.Container)'."

    if (-not $DiscardOutput) {
        return @($output)
    }
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

function Wait-DipPostgresReady {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Client,

        [int]$TimeoutSeconds = 120,

        [int]$RetryMilliseconds = 1000
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempt = 0

    while ((Get-Date) -lt $deadline) {
        $attempt++

        if (Test-DipPostgresReady -Client $Client) {
            return [pscustomobject]@{
                Ready = $true
                Attempts = $attempt
            }
        }

        Start-Sleep -Milliseconds $RetryMilliseconds
    }

    return [pscustomobject]@{
        Ready = $false
        Attempts = $attempt
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
