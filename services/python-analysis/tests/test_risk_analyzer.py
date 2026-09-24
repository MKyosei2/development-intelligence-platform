# Purpose: Implements test risk analyzer responsibilities for python analysis.
from app.schemas.risk import RiskFactorInput, RiskRequest
from app.services.risk_analyzer import RiskAnalyzer

def test_build_failure_and_regressions_create_high_risk() -> None:
    request = RiskRequest(
        project_id="p1",
        pull_request_id="128",
        factors=RiskFactorInput(
            changed_files=70,
            changed_lines=5000,
            build_failed=True,
            test_failures=2,
            fps_regression_percent=-8,
            memory_regression_percent=25,
            review_wait_hours=30,
            critical_issues=1,
            warning_issues=3,
        ),
    )
    result = RiskAnalyzer().analyze(request)
    assert result.score >= 60
    assert any(item.factor == "build_failed" for item in result.contributions)
