# Local Development

```powershell
docker compose up -d postgres redis minio python-analysis
dotnet ef database update --project src/Dip.Infrastructure --startup-project src/Dip.Api
dotnet run --project src/Dip.Api
dotnet run --project src/Dip.Web
dotnet run --project src/Dip.Worker
```
