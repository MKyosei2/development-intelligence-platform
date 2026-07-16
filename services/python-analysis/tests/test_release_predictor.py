from app.core.config import Settings
from app.schemas.release_prediction import ReleasePredictionRequest
from app.services.release_predictor import ReleasePredictor

def test_overdue_and_blocked_tasks_reduce_confidence() -> None:
    predictor = ReleasePredictor(Settings(release_base_confidence=90))
    request = ReleasePredictionRequest(
        project_id="p1",
        days_until_release=14,
        open_tasks=40,
        overdue_tasks=8,
        blocked_tasks=4,
        critical_issues=2,
        failed_builds_last_7_days=3,
        average_review_wait_hours=36,
        weekly_velocity=10,
    )
    result = predictor.predict(request)
    assert result.confidence < 60
    assert result.estimated_delay_days > 0
