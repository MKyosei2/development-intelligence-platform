from app.schemas.common import EvidenceItem
from app.schemas.risk import RiskContribution, RiskRequest, RiskResponse

class RiskAnalyzer:
    def analyze(self, request: RiskRequest) -> RiskResponse:
        f = request.factors
        contributions: list[RiskContribution] = []
        evidence: list[EvidenceItem] = []

        def add(name: str, points: float, explanation: str, value: float | int | str) -> None:
            if points <= 0:
                return
            contributions.append(RiskContribution(factor=name, points=round(points, 2), explanation=explanation))
            evidence.append(EvidenceItem(
                evidence_id=f"risk:{request.pull_request_id}:{name}",
                source_type="pull_request_metric",
                description=explanation,
                value=value,
            ))

        add("changed_files", min(f.changed_files / 5, 12), "Large number of changed files.", f.changed_files)
        add("changed_lines", min(f.changed_lines / 500, 12), "Large code or asset diff.", f.changed_lines)
        add("build_failed", 25 if f.build_failed else 0, "The current build failed.", str(f.build_failed))
        add("test_failures", min(f.test_failures * 5, 20), "Automated tests failed.", f.test_failures)
        add("fps_regression", min(max(-f.fps_regression_percent, 0) * 1.5, 15), "Average FPS regressed.", f.fps_regression_percent)
        add("memory_regression", min(max(f.memory_regression_percent, 0) * 0.75, 15), "Memory usage increased.", f.memory_regression_percent)
        add("review_wait", min(max(f.review_wait_hours - 12, 0) / 4, 8), "Review waiting time is elevated.", f.review_wait_hours)
        add("critical_issues", min(f.critical_issues * 12, 24), "Critical issues remain open.", f.critical_issues)
        add("warning_issues", min(f.warning_issues * 2, 10), "Warning issues remain open.", f.warning_issues)

        score = min(sum(item.points for item in contributions), 100.0)
        level = "critical" if score >= 80 else "high" if score >= 60 else "medium" if score >= 30 else "low"
        return RiskResponse(
            score=round(score, 2),
            level=level,
            contributions=sorted(contributions, key=lambda item: item.points, reverse=True),
            evidence=evidence,
        )
