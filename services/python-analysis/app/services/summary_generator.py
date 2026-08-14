from app.schemas.summary import SummaryInput, SummaryResponse
from app.services.evidence_builder import EvidenceBuilder


class SummaryGenerator:
    def generate(self, request: SummaryInput) -> SummaryResponse:
        risks: list[str] = []
        actions: list[str] = []
        if request.failed_builds > 0:
            risks.append(f"{request.failed_builds} build(s) are failing.")
            actions.append("Assign the latest build failure and verify the failing stage.")
        if request.test_failures > 0:
            risks.append(f"{request.test_failures} automated test(s) are failing.")
            actions.append("Fix or quarantine reproducible failing tests before merge.")
        if request.critical_issues > 0:
            risks.append(f"{request.critical_issues} critical quality issue(s) remain open.")
            actions.append("Review critical quality issues and assign owners.")
        if request.blocked_tasks > 0:
            risks.append(f"{request.blocked_tasks} task(s) are blocked.")
            actions.append("Resolve blockers or reduce scope.")
        if request.overloaded_members > 0:
            risks.append(f"{request.overloaded_members} team member(s) appear overloaded.")
            actions.append("Redistribute reviews and high-priority tasks.")
        if request.fps_regression_percent <= -5:
            risks.append(f"Average FPS regressed by {abs(request.fps_regression_percent):.1f}%.")
            actions.append("Inspect performance regressions and the responsible changes.")
        if request.memory_regression_percent >= 10:
            risks.append(f"Memory usage increased by {request.memory_regression_percent:.1f}%.")
            actions.append("Review recently added assets and allocations.")
        if not risks:
            risks.append("No major risk was detected from the supplied project metrics.")
            actions.append("Continue monitoring trends and verify the next pipeline run.")
        overall_status = (
            "critical" if request.health_score < 40 or request.critical_issues > 0
            else "at_risk" if request.health_score < 70 or len(risks) >= 3
            else "stable"
        )
        return SummaryResponse(
            overall_status=overall_status,
            top_risks=risks[:5],
            recommended_actions=actions[:5],
            evidence_ids=EvidenceBuilder.ids(request.evidence),
        )
