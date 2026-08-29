# DIP Python Analysis Service

The analysis service is a normal Python process. Docker is not required.

```powershell
cd services/python-analysis
python -m venv .venv
.\.venv\Scripts\python -m pip install -r requirements.txt
$env:API_KEY = "development-key"
.\.venv\Scripts\python -m uvicorn app.main:app --host 127.0.0.1 --port 8100
```

On Linux use `.venv/bin/python` instead. `scripts/Start-DIP.ps1` creates and manages the repository-level native venv automatically for the complete DIP stack.

Health: `GET http://127.0.0.1:8100/health`.
