# Purpose: Implements release predictor responsibilities for app.
import math

from app.core.config import Settings
from app.schemas.release_prediction import ReleasePredictionRequest, ReleasePredictionResponse
from app.services.evidence_builder import EvidenceBuilder


class ReleasePredictor:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def predict(self, request: ReleasePredictionRequest) -> ReleasePredictionResponse:
        factors: list[str] = []
        penalty = 0.0
        if request.days_until_release <= 0:
            penalty += 40
            factors.append("The planned release date has arrived or passed.")
        task_pressure = request.open_tasks / max(request.weekly_velocity, 1.0)
        available_weeks = max(request.days_until_release / 7.0, 0.1)
        if task_pressure > available_weeks:
            penalty += min((task_pressure - available_weeks) * 8, 30)
            factors.append("Open task volume exceeds the current delivery velocity.")
        penalty += min(request.overdue_tasks * 2.5, 20)
        penalty += min(request.blocked_tasks * 4.0, 20)
        penalty += min(request.critical_issues * 7.5, 30)
        penalty += min(request.failed_builds_last_7_days * 3.0, 15)
        penalty += min(max(request.average_review_wait_hours - 24, 0) / 4, 10)
        if request.overdue_tasks:
            factors.append(f"{request.overdue_tasks} task(s) are overdue.")
        if request.blocked_tasks:
            factors.append(f"{request.blocked_tasks} task(s) are blocked.")
        if request.critical_issues:
            factors.append(f"{request.critical_issues} critical issue(s) remain unresolved.")
        if request.failed_builds_last_7_days:
            factors.append("Recent build instability reduces release confidence.")
        if request.average_review_wait_hours > 24:
            factors.append("Average review wait exceeds 24 hours.")
        confidence = max(0.0, min(100.0, self._settings.release_base_confidence - penalty))
        if request.weekly_velocity > 0:
            delay_days = max(
                0,
                math.ceil(
                    request.open_tasks / request.weekly_velocity * 7
                    - max(request.days_until_release, 0)
                ),
            )
        elif request.open_tasks > 0:
            delay_days = max(request.days_until_release, 0) + 30
        else:
            delay_days = 0
        risk_level = (
            "critical" if confidence < 35
            else "high" if confidence < 60
            else "medium" if confidence < 80
            else "low"
        )
        if not factors:
            factors.append("No significant release risk factor was detected.")
        return ReleasePredictionResponse(
            confidence=round(confidence, 2),
            estimated_delay_days=delay_days,
            risk_level=risk_level,
            factors=factors[:6],
            evidence_ids=EvidenceBuilder.ids(request.evidence),
        )
