$ErrorActionPreference = "Stop"

$container = docker compose ps -q postgres
if (-not $container) {
    throw "PostgreSQL container is not running."
}

Get-Content infrastructure/database/seed/sample-project.sql -Raw |
  docker exec -i $container psql -U dip -d dip

Write-Host "Sample data inserted."
