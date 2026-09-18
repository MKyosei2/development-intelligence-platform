#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
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
            body = json.loads(raw) if raw.strip() else None
            return response.status, body
    except HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {exc.code} {method} {url}: {body}") from exc
    except URLError as exc:
        raise RuntimeError(f"Could not reach DIP API at {url}: {exc}") from exc


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Ingest an actual engine collector report into DIP and fail closed unless "
            "the resulting Project Truth matches the expected clean real-project path."
        )
    )
    parser.add_argument("--api-base", default="http://127.0.0.1:5080")
    parser.add_argument("--engine-evidence", required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--expected-dip-sha", required=True)
    parser.add_argument("--project-name")
    parser.add_argument("--output", default="artifacts/real-project-runtime/project-truth")
    args = parser.parse_args()

    api_base = args.api_base.rstrip("/")
    evidence_path = Path(args.engine_evidence).resolve()
    report_path = Path(args.report).resolve()
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)

    evidence = json.loads(evidence_path.read_text(encoding="utf-8-sig"))
    report = json.loads(report_path.read_text(encoding="utf-8-sig"))

    if evidence.get("dipSourceCommit", "").lower() != args.expected_dip_sha.lower():
        raise RuntimeError(
            "Engine evidence is not bound to the expected DIP SHA: "
            f"expected={args.expected_dip_sha} actual={evidence.get('dipSourceCommit')}"
        )
    if not evidence.get("runtimeVerified"):
        raise RuntimeError("Engine evidence does not prove a real runtime execution.")
    if evidence.get("qualityGatePassed") is not True:
        raise RuntimeError(
            "The real engine executed, but its quality gate did not PASS. "
            "Runtime verification is not equivalent to release readiness."
        )

    target_repository = evidence.get("targetRepository") or report.get("repository")
    target_branch = evidence.get("targetBranch") or report.get("branch") or "main"
    target_commit = evidence.get("targetCommit") or report.get("commitSha")
    if not target_repository or not target_commit:
        raise RuntimeError("Target repository/commit identity is incomplete.")

    report_commit = str(report.get("commitSha") or "")
    if report_commit.lower() != str(target_commit).lower():
        raise RuntimeError(
            "Engine evidence and collector report refer to different target revisions: "
            f"evidence={target_commit} report={report_commit}"
        )

    project_name = args.project_name or (
        f"Portfolio Case Study - {evidence.get('targetProject', report.get('collectorType', 'engine'))}"
    )
    status, project = request_json(
        "POST",
        f"{api_base}/api/projects",
        {
            "name": project_name,
            "repositoryUrl": target_repository,
            "defaultBranch": target_branch,
            "releaseDateUtc": None,
        },
    )
    if status not in {200, 201} or not isinstance(project, dict) or not project.get("id"):
        raise RuntimeError(
            f"Project creation returned an unexpected response: status={status} body={project!r}"
        )

    project_id = project["id"]
    normalized = dict(report)
    normalized["projectId"] = project_id
    normalized["repository"] = target_repository
    normalized["commitSha"] = target_commit
    normalized["branch"] = target_branch
    normalized["externalRunId"] = (
        str(report.get("externalRunId") or f"real-engine-{uuid.uuid4().hex}")
        + "-ingest-"
        + uuid.uuid4().hex[:12]
    )
    normalized["attemptNumber"] = 1

    status_map = {
        "succeeded": "Succeeded",
        "success": "Succeeded",
        "warning": "PartialSuccess",
        "partialsuccess": "PartialSuccess",
        "failed": "Failed",
    }
    normalized_status = status_map.get(
        str(normalized.get("status", "")).replace(" ", "").lower()
    )
    if normalized_status is None:
        raise RuntimeError(
            f"Collector report status is not API compatible: {normalized.get('status')!r}"
        )
    normalized["status"] = normalized_status

    # Final clean-project publication evidence must be an actual successful
    # collector run, not merely a runtime execution that returned findings.
    if normalized_status != "Succeeded":
        raise RuntimeError(
            f"Final real-project collector status must be Succeeded; actual={normalized_status!r}"
        )

    normalized_path = output / "normalized-engine-report.json"
    normalized_path.write_text(
        json.dumps(normalized, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    ingest_status, ingest = request_json(
        "POST",
        f"{api_base}/api/v1/collector-reports",
        normalized,
    )
    if ingest_status not in {200, 201}:
        raise RuntimeError(
            f"Collector ingestion failed: status={ingest_status} body={ingest!r}"
        )
    if not isinstance(ingest, dict):
        raise RuntimeError(f"Collector ingestion returned an unexpected body: {ingest!r}")
    if (ingest.get("qualityGate") or {}).get("status") not in {None, "Passed"}:
        raise RuntimeError(
            "Collector ingestion did not produce a Passed quality gate: "
            f"{ingest.get('qualityGate')!r}"
        )

    claim_status, claim = request_json(
        "POST",
        f"{api_base}/api/v1/projects/{project_id}/claims",
        {
            "subjectKey": "project",
            "claimedStatus": "Complete",
            "claimedProgress": 100,
            "claimedBy": "portfolio-real-project-verification",
            "description": (
                "Portfolio case-study claim submitted after real engine collector evidence ingestion."
            ),
        },
    )
    if claim_status not in {200, 201} or not isinstance(claim, dict):
        raise RuntimeError(
            f"Project claim creation failed: status={claim_status} body={claim!r}"
        )

    dashboard_status, dashboard = request_json(
        "GET",
        f"{api_base}/api/v1/dashboard/projects/{project_id}",
    )
    if dashboard_status != 200 or not isinstance(dashboard, dict):
        raise RuntimeError(
            f"Dashboard evidence fetch failed: status={dashboard_status} body={dashboard!r}"
        )

    # Positive-path assertions. Merely obtaining a JSON object is not evidence
    # that the three pillars agree with each other.
    if dashboard.get("qualityGateStatus") != "Passed":
        raise RuntimeError(
            f"Project Truth quality gate is not Passed: {dashboard.get('qualityGateStatus')!r}"
        )
    if claim.get("verificationStatus") != "Confirmed":
        raise RuntimeError(
            "The 100% Complete claim is not Confirmed by the ingested current-revision evidence: "
            f"{claim.get('verificationStatus')!r}"
        )
    if int(dashboard.get("contradictionCount") or 0) != 0:
        raise RuntimeError(
            f"Project Truth contains contradictions: {dashboard.get('contradictionCount')!r}"
        )

    bundle = {
        "schema": "dip.real-project.project-truth.v2",
        "dipSourceCommit": args.expected_dip_sha,
        "engine": evidence.get("schema"),
        "targetProject": evidence.get("targetProject"),
        "targetRepository": target_repository,
        "targetCommit": target_commit,
        "projectId": project_id,
        "collectorIngestion": ingest,
        "claim": claim,
        "projectTruth": dashboard,
        "assertions": {
            "engineRuntimeVerified": True,
            "engineQualityGatePassed": True,
            "collectorStatusSucceeded": True,
            "projectTruthQualityGatePassed": True,
            "completionClaimConfirmed": True,
            "contradictionCountZero": True,
        },
        "capturedAtUtc": datetime.now(timezone.utc).isoformat(),
        "sourceEngineEvidence": str(evidence_path),
        "normalizedCollectorReport": str(normalized_path),
    }
    bundle_path = output / "project-truth-evidence.json"
    bundle_path.write_text(
        json.dumps(bundle, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    print("REAL PROJECT -> DIP -> PROJECT TRUTH: PASS")
    print(f"DIP SHA: {args.expected_dip_sha}")
    print(f"Target: {target_repository}@{target_commit}")
    print("QualityGateStatus: Passed")
    print("ClaimVerificationStatus: Confirmed")
    print("ContradictionCount: 0")
    print(f"Evidence: {bundle_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
