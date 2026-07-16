param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [int]$TimeoutSeconds = 60
)

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)

while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
            Write-Host "Service is available: $Url"
            exit 0
        }
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

throw "Timed out waiting for $Url"
