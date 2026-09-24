# Purpose: Implements test summary generator responsibilities for python analysis.
from app.schemas.summary import SummaryInput
from app.services.summary_generator import SummaryGenerator

def test_summary_contains_build_and_memory_actions() -> None:
    request = SummaryInput(
        project_id="p1",
        project_name="Aurora",
        health_score=55,
        failed_builds=2,
        memory_regression_percent=22,
    )
    result = SummaryGenerator().generate(request)
    assert result.overall_status == "at_risk"
    assert any("build" in item.lower() for item in result.top_risks)
    assert any("memory" in item.lower() for item in result.top_risks)
