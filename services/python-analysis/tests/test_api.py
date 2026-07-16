from fastapi.testclient import TestClient
from app.main import app

client = TestClient(app)

def test_health() -> None:
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "healthy"}

def test_risk_endpoint() -> None:
    response = client.post(
        "/api/analysis/risk",
        json={
            "project_id": "p1",
            "pull_request_id": "12",
            "factors": {
                "changed_files": 10,
                "changed_lines": 300,
                "build_failed": False,
                "test_failures": 0,
                "fps_regression_percent": -2,
                "memory_regression_percent": 3,
                "review_wait_hours": 4,
                "critical_issues": 0,
                "warning_issues": 1
            }
        },
    )
    assert response.status_code == 200
