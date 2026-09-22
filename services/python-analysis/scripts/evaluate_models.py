# Purpose: Defines a focused responsibility within the development intelligence platform.
from app.schemas.risk import RiskFactorInput, RiskRequest
from app.services.risk_analyzer import RiskAnalyzer

def main() -> None:
    samples = [
        ("safe", RiskFactorInput(changed_files=3, changed_lines=50)),
        ("warning", RiskFactorInput(changed_files=30, changed_lines=1500, warning_issues=4)),
        ("critical", RiskFactorInput(changed_files=100, changed_lines=10000, build_failed=True, test_failures=5, critical_issues=2)),
    ]
    analyzer = RiskAnalyzer()
    for name, factors in samples:
        result = analyzer.analyze(RiskRequest(project_id="sample", pull_request_id=name, factors=factors))
        print(name, result.score, result.level)

if __name__ == "__main__":
    main()
