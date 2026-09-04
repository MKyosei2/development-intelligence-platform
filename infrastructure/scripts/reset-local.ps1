param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if (-not $Force) {
    $confirmation = Read-Host "This deletes local PostgreSQL and MinIO data. Type RESET"
    if ($confirmation -ne "RESET") {
        Write-Host "Cancelled."
        exit 1
    }
}

docker compose down -v
docker compose up -d postgres minio python-analysis
Write-Host "Local environment reset."
