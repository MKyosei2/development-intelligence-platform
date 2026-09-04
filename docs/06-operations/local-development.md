# Local development

DIP uses a Native-first local runtime. Install .NET 8, Python 3.12+, PostgreSQL and PostgreSQL client tools. Create the `dip` database/user once, set `POSTGRES_*` variables (see `.env.example`), then run:

```powershell
.\scripts\Start-DIP.ps1
```

The launcher creates the Python venv and starts Analysis/API/Worker/Web directly. PID/log state is under `.dip/runtime`. Stop with `scripts/Stop-DIP.ps1 -KeepData`. Docker and an external queue daemon are not required.
