# DIP Python Analysis Service

Analysis Serviceは通常のPython processとして動作します。Dockerは必須ではありません。

```powershell
cd services/python-analysis
python -m venv .venv
.\.venv\Scripts\python -m pip install -r requirements.txt
$env:API_KEY = "development-key"
.\.venv\Scripts\python -m uvicorn app.main:app --host 127.0.0.1 --port 8100
```

Linuxでは `.venv/bin/python` を使用してください。DIP全体を起動する場合、`scripts/Start-DIP.ps1` がRepository-levelのnative venvを自動的に作成・管理します。

Health check: `GET http://127.0.0.1:8100/health`
