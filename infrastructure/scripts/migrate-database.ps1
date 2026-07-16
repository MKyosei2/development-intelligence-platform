$ErrorActionPreference = "Stop"

dotnet tool restore
dotnet ef database update `
  --project src/Dip.Infrastructure `
  --startup-project src/Dip.Api

Write-Host "Database migration completed."
