#!/usr/bin/env python3
# Purpose: Requires all 11 publication workflows to be Green for one exact source revision.
from __future__ import annotations

import argparse
import json
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

REQUIRED = [
    "Commit Message",
    ".NET CI",
    "Python CI",
    "Native Collector CI",
    "Integration and E2E Tests",
    "GUI E2E",
    "Native Package Build",
    "Security Scan",
    "Universal QA Benchmark",
    "Unreal Collector CI",
    "Portfolio Publication Readiness",
]


def run(cmd: list[str], check: bool = True) -> subprocess.CompletedProcess[str]:
    cp = subprocess.run(cmd, text=True, capture_output=True, check=False)
    if check and cp.returncode != 0:
        raise RuntimeError(
            f"Command failed ({cp.returncode}): {subprocess.list2cmdline(cmd)}\n{cp.stdout}\n{cp.stderr}"
        )
    return cp


def git_sha() -> str:
    return run(["git", "rev-parse", "HEAD"]).stdout.strip()


def load_runs(repo: str, sha: str) -> list[dict]:
    endpoint = f"repos/{repo}/actions/runs?head_sha={sha}&per_page=100"
    cp = run(["gh", "api", endpoint])
    payload = json.loads(cp.stdout)
    return list(payload.get("workflow_runs", []))


def latest_for_name(runs: list[dict], name: str, sha: str) -> dict | None:
    matches = [item for item in runs if item.get("name") == name and item.get("head_sha") == sha]
    if not matches:
        return None
    matches.sort(
        key=lambda item: (
            item.get("run_number") or 0,
            item.get("run_attempt") or 0,
            item.get("created_at") or "",
        ),
        reverse=True,
    )
    return matches[0]


def write_snapshot(output: Path, sha: str, matrix: dict[str, dict], missing: list[str], pending: list[str], failed: list[str]) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "schema": "dip.final-ci-matrix.v2",
        "sourceCommit": sha,
        "requiredCount": len(REQUIRED),
        "greenCount": sum(1 for item in matrix.values() if item.get("conclusion") == "success"),
        "allGreen": not missing and not pending and not failed and len(matrix) == len(REQUIRED),
        "verifiedAtUtc": datetime.now(timezone.utc).isoformat(),
        "missing": missing,
        "pending": pending,
        "failed": failed,
        "workflows": [matrix[name] for name in REQUIRED if name in matrix],
    }
    output.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Require the 11 publication workflows to be Green for one exact SHA.")
    parser.add_argument("--repo", default="MKyosei2/development-intelligence-platform")
    parser.add_argument("--sha", default=None)
    parser.add_argument("--wait-minutes", type=int, default=0)
    parser.add_argument("--poll-seconds", type=int, default=20)
    parser.add_argument("--output", default="artifacts/final-evidence/ci-matrix.json")
    args = parser.parse_args()

    run(["gh", "auth", "status"])
    sha = args.sha or git_sha()
    if len(sha) != 40:
        raise RuntimeError(f"Invalid SHA: {sha!r}")

    deadline = time.time() + max(0, args.wait_minutes) * 60
    output = Path(args.output)

    while True:
        runs = load_runs(args.repo, sha)
        missing: list[str] = []
        pending: list[str] = []
        failed: list[str] = []
        matrix: dict[str, dict] = {}

        for name in REQUIRED:
            item = latest_for_name(runs, name, sha)
            if item is None:
                missing.append(name)
                continue

            record = {
                "workflow": name,
                "runId": item.get("id"),
                "runNumber": item.get("run_number"),
                "runAttempt": item.get("run_attempt"),
                "event": item.get("event"),
                "headSha": item.get("head_sha"),
                "status": item.get("status"),
                "conclusion": item.get("conclusion"),
                "url": item.get("html_url"),
                "createdAt": item.get("created_at"),
                "updatedAt": item.get("updated_at"),
            }
            matrix[name] = record

            if item.get("status") != "completed":
                pending.append(name)
            elif item.get("conclusion") != "success":
                failed.append(f"{name}={item.get('conclusion')}")

        write_snapshot(output, sha, matrix, missing, pending, failed)

        print(f"Exact SHA: {sha}")
        print(f"Green: {sum(1 for item in matrix.values() if item.get('conclusion') == 'success')}/{len(REQUIRED)}")
        if missing:
            print("Missing:", ", ".join(missing))
        if pending:
            print("Pending:", ", ".join(pending))
        if failed:
            print("Failed:", ", ".join(failed))
        print(f"Evidence snapshot: {output}")

        if failed:
            raise RuntimeError(
                "A required exact-SHA workflow is not Green. Failure/cancel/skip is never reclassified as PASS: "
                + ", ".join(failed)
            )
        if not missing and not pending:
            print(f"PASS: all {len(REQUIRED)} required workflows are Green for {sha}.")
            return 0
        if time.time() >= deadline:
            raise RuntimeError(
                "Exact-SHA CI matrix is incomplete. "
                + ("Missing: " + ", ".join(missing) + ". " if missing else "")
                + ("Pending: " + ", ".join(pending) + "." if pending else "")
            )

        time.sleep(max(5, args.poll_seconds))


if __name__ == "__main__":
    raise SystemExit(main())
