$ErrorActionPreference = "Stop"

Write-Host "Starting DIP local infrastructure..."
docker compose up -d postgres redis python-analysis

Write-Host "Waiting for services..."
Start-Sleep -Seconds 5

docker compose ps
Write-Host "Local infrastructure started."
