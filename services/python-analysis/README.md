# DIP Python Analysis Service

## Endpoints

- `GET /health`
- `POST /api/analysis/anomaly`
- `POST /api/analysis/risk`
- `POST /api/analysis/summary`
- `POST /api/analysis/release-prediction`

## Local setup

```powershell
cd services/python-analysis
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements.txt
pip install -r requirements-dev.txt
uvicorn app.main:app --reload --port 8100
```

Swagger: `http://localhost:8100/docs`

Tests:

```powershell
pytest
ruff check app tests scripts
mypy app
```

Set `API_KEY` to require the `X-API-Key` header.
The service does not write to the C# backend database and does not decide final quality-gate pass/fail.
