#!/usr/bin/env python3
# Purpose: Publishes deterministic Critical native/Universal-QA evidence into DIP and proves Project Truth rejects an unsupported Complete claim.
from __future__ import annotations

import argparse
import json
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def request_json(method: str, url: str, payload: object | None = None) -> tuple[int, object]:
    data = None
    headers = {"Accept": "application/json"}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = Request(url, data=data, headers=headers, method=method)
    try:
        with urlopen(request, timeout=30) as response:
            raw = response.read().decode("utf-8")
            return response.status, json.loads(raw) if raw.strip() else None
    except HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {exc.code} {method} {url}: {body}") from exc
    except URLError as exc:
        raise RuntimeError(f"Could not reach DIP API at {url}: {exc}") from exc


def dashboard_assertions(dashboard: dict) -> dict[str, bool]:
    reported = dashboard.get("reportedProgress")
    verified = dashboard.get("verifiedProgress")
    return {
        "qualityGateFailed": dashboard.get("qualityGateStatus") == "Failed",
        "verifiedProgressBelowReported": (
            isinstance(reported, (int, float))
            and isinstance(verified, (int, float))
            and verified < reported
        ),
        "contradictionRecorded": int(dashboard.get("contradictionCount") or 0) > 0,
    }


def wait_for_project_truth_dashboard(
    api_base: str,
    project_id: str,
    *,
    max_attempts: int = 120,
    poll_interval_seconds: float = 0.5,
) -> tuple[dict, dict[str, bool], int]:
    if max_attempts < 1:
        raise ValueError("max_attempts must be at least 1")
    if poll_interval_seconds < 0:
        raise ValueError("poll_interval_seconds must be non-negative")

    last_dashboard: dict = {}
    last_assertions: dict[str, bool] = {}

    for attempt in range(1, max_attempts + 1):
        status, dashboard = request_json(
            "GET",
            f"{api_base}/api/v1/dashboard/projects/{project_id}",
        )
        if status == 200 and isinstance(dashboard, dict):
            last_dashboard = dashboard
            last_assertions = dashboard_assertions(dashboard)
            if all(last_assertions.values()):
                return dashboard, last_assertions, attempt
        else:
            last_dashboard = {
                "unexpectedStatus": status,
                "body": dashboard,
            }
            last_assertions = {}

        if attempt < max_attempts:
            time.sleep(poll_interval_seconds)

    raise RuntimeError(
        "Project Truth dashboard did not converge after asynchronous quality-gate processing: "
        f"attempts={max_attempts} assertions={json.dumps(last_assertions)} "
        f"dashboard={json.dumps(last_dashboard)}"
    )

def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Publish a deterministic Critical native QA finding into DIP and require Project Truth "
            "to fail the quality gate and contradict a 100% Complete claim."
        )
    )
    parser.add_argument("--api-base", default="http://127.0.0.1:5080")
    parser.add_argument("--expected-dip-sha", required=True)
    parser.add_argument("--repository", default="https://github.com/MKyosei2/development-intelligence-platform")
    parser.add_argument("--branch", default="main")
    parser.add_argument("--project-name", default="DIP Native QA Project Truth Scenario")
    parser.add_argument("--dashboard-max-attempts", type=int, default=120)
    parser.add_argument("--dashboard-poll-interval-seconds", type=float, default=0.5)
    parser.add_argument("--output", default="artifacts/qa-project-truth")
    args = parser.parse_args()

    expected = args.expected_dip_sha.lower()
    if len(expected) != 40 or any(ch not in "0123456789abcdef" for ch in expected):
        raise RuntimeError(f"Invalid expected DIP SHA: {args.expected_dip_sha!r}")

    api_base = args.api_base.rstrip("/")
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)

    status, project = request_json(
        "POST",
        f"{api_base}/api/projects",
        {
            "name": args.project_name,
            "repositoryUrl": args.repository,
            "defaultBranch": args.branch,
            "releaseDateUtc": None,
        },
    )
    if status not in {200, 201} or not isinstance(project, dict) or not project.get("id"):
        raise RuntimeError(f"Project creation returned an unexpected response: status={status} body={project!r}")

    project_id = project["id"]
    now = datetime.now(timezone.utc)
    report = {
        "schemaVersion": "1.0",
        "reportId": f"qa-scenario-{uuid.uuid4().hex}",
        "externalRunId": f"qa-scenario-{expected[:12]}-{uuid.uuid4().hex}",
        "attemptNumber": 1,
        "collectorType": "native-qa-scenario",
        "collectorVersion": "1.0.0",
        "projectId": project_id,
        "repository": args.repository,
        "commitSha": expected,
        "branch": args.branch,
        "startedAt": now.isoformat(),
        "finishedAt": now.isoformat(),
        "status": "Succeeded",
        "metrics": [],
        "issues": [
            {
                "category": "memory-safety",
                "severity": "Critical",
                "title": "Native QA detected an intentional memory-safety regression",
                "description": (
                    "Deterministic portfolio scenario: a Critical native/Universal-QA finding is injected "
                    "to prove that Project Truth does not accept an unsupported Complete claim."
                ),
                "source": "dip-qa-scenario",
                "ruleId": "DIP-SCENARIO-NATIVE-CRITICAL-001",
                "evidenceJson": json.dumps(
                    {
                        "scenario": "native-critical-contradiction",
                        "dipSourceCommit": expected,
                        "synthetic": True,
                        "purpose": "deterministic Project Truth expected-failure verification",
                    }
                ),
            }
        ],
    }

    status, ingestion = request_json("POST", f"{api_base}/api/v1/collector-reports", report)
    if status not in {200, 201} or not isinstance(ingestion, dict):
        raise RuntimeError(f"Collector ingestion failed: status={status} body={ingestion!r}")

    gate = ingestion.get("qualityGate") or {}
    if gate.get("status") != "Failed":
        raise RuntimeError(f"Intentional Critical QA report did not fail the quality gate: {gate!r}")

    status, claim = request_json(
        "POST",
        f"{api_base}/api/v1/projects/{project_id}/claims",
        {
            "subjectKey": "project",
            "claimedStatus": "Complete",
            "claimedProgress": 100,
            "claimedBy": "portfolio-qa-scenario",
            "description": "Intentional unsupported Complete claim for Project Truth contradiction verification.",
        },
    )
    if status not in {200, 201} or not isinstance(claim, dict):
        raise RuntimeError(f"Project claim creation failed: status={status} body={claim!r}")

    dashboard, dashboard_checks, convergence_attempts = wait_for_project_truth_dashboard(
        api_base,
        project_id,
        max_attempts=args.dashboard_max_attempts,
        poll_interval_seconds=args.dashboard_poll_interval_seconds,
    )

    claim_contradicted = claim.get("verificationStatus") == "Contradicted"
    assertions = {
        "qualityGateFailed": dashboard_checks["qualityGateFailed"],
        "completionClaimContradicted": claim_contradicted,
        "verifiedProgressBelowReported": dashboard_checks["verifiedProgressBelowReported"],
        "contradictionRecorded": dashboard_checks["contradictionRecorded"],
    }
    if not all(assertions.values()):
        raise RuntimeError("Project Truth expected-failure assertions did not all pass: " + json.dumps(assertions))

    bundle = {
        "schema": "dip.qa-project-truth-scenario.v1",
        "status": "PASS",
        "dipSourceCommit": expected,
        "scenario": "native-critical-contradicts-complete-claim",
        "scenarioSemantics": "Expected product quality failure; verifier PASS means DIP correctly rejected the unsupported completion claim.",
        "projectId": project_id,
        "repository": args.repository,
        "collectorIngestion": ingestion,
        "claim": claim,
        "projectTruth": dashboard,
        "dashboardConvergenceAttempts": convergence_attempts,
        "assertions": assertions,
        "capturedAtUtc": datetime.now(timezone.utc).isoformat(),
    }
    bundle_path = output / "project-truth-evidence.json"
    bundle_path.write_text(json.dumps(bundle, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print("QA SCENARIO -> DIP -> PROJECT TRUTH: PASS")
    print(f"DIP SHA: {expected}")
    print("Expected product gate: Failed")
    print("Expected Complete claim: Contradicted")
    print(f"Dashboard convergence attempts: {convergence_attempts}")
    print(f"Evidence: {bundle_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
