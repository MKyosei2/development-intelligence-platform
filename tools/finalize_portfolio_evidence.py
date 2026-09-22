#!/usr/bin/env python3
# Purpose: Provides repository tooling used by development and verification workflows.
from __future__ import annotations

import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path


def load(path: Path) -> dict:
    if not path.is_file():
        raise RuntimeError(f"Required evidence file is missing: {path}")
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise RuntimeError(f"Evidence root must be a JSON object: {path}")
    return value


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_sha(label: str, value: object, expected: str) -> None:
    if str(value or "").lower() != expected.lower():
        raise RuntimeError(
            f"{label} is not bound to the final DIP SHA. expected={expected} actual={value}"
        )


def require_true(label: str, value: object) -> None:
    if value is not True:
        raise RuntimeError(f"{label} must be true for final publication evidence; actual={value!r}")


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Fail closed unless final CI, benchmark, engine runtime/quality, and Project Truth "
            "evidence all belong to one exact DIP SHA."
        )
    )
    parser.add_argument("--sha", required=True)
    parser.add_argument("--ci", default="artifacts/final-evidence/ci-matrix.json")
    parser.add_argument("--benchmark", default="artifacts/portfolio-benchmark/portfolio-benchmark-results.json")
    parser.add_argument("--unity", default="artifacts/real-project-runtime/unity/unity-real-project-evidence.json")
    parser.add_argument("--unreal", default="artifacts/real-project-runtime/unreal/unreal-real-project-evidence.json")
    parser.add_argument(
        "--project-truth",
        required=True,
        help="Project Truth evidence is mandatory for final three-pillar publication evidence.",
    )
    parser.add_argument("--output-root", default="artifacts/final-evidence")
    args = parser.parse_args()

    expected = args.sha.lower()
    if len(expected) != 40 or any(ch not in "0123456789abcdef" for ch in expected):
        raise RuntimeError(f"Invalid final SHA: {args.sha!r}")

    paths = {
        "ci": Path(args.ci).resolve(),
        "benchmark": Path(args.benchmark).resolve(),
        "unity": Path(args.unity).resolve(),
        "unreal": Path(args.unreal).resolve(),
        "projectTruth": Path(args.project_truth).resolve(),
    }

    ci = load(paths["ci"])
    benchmark = load(paths["benchmark"])
    unity = load(paths["unity"])
    unreal = load(paths["unreal"])
    project_truth = load(paths["projectTruth"])

    # 1) Exact-revision CI.
    require_sha("CI matrix", ci.get("sourceCommit"), expected)
    if ci.get("requiredCount") != 11 or ci.get("greenCount") != 11 or ci.get("allGreen") is not True:
        raise RuntimeError("Final CI evidence is not an 11/11 Green matrix.")

    # 2) Exact-revision benchmark.
    require_sha("Benchmark", benchmark.get("sourceCommit"), expected)
    if benchmark.get("sourceWorkingTreeDirty") is not False:
        raise RuntimeError("Benchmark was measured from a dirty working tree.")
    require_true("Benchmark passed", benchmark.get("passed"))

    # 3) Real Unity runtime AND quality gate.
    require_sha("Unity evidence", unity.get("dipSourceCommit"), expected)
    require_true("Unity runtimeVerified", unity.get("runtimeVerified"))
    require_true("Unity qualityGatePassed", unity.get("qualityGatePassed"))
    if not unity.get("targetRepository") or not unity.get("targetCommit"):
        raise RuntimeError("Unity target project identity is incomplete.")

    # 4) Real Unreal runtime AND quality gate.
    require_sha("Unreal evidence", unreal.get("dipSourceCommit"), expected)
    require_true("Unreal runtimeVerified", unreal.get("runtimeVerified"))
    require_true("Unreal qualityGatePassed", unreal.get("qualityGatePassed"))
    engine = unreal.get("engineNativeAutomation") or {}
    if engine.get("attempted") is not True or engine.get("timedOut") is True or not engine.get("report"):
        raise RuntimeError("Unreal native Automation execution evidence is incomplete.")

    # 5) Real Project -> DIP -> Project Truth. Presence alone is not sufficient.
    require_sha("Project Truth case-study evidence", project_truth.get("dipSourceCommit"), expected)
    if project_truth.get("targetRepository") != unity.get("targetRepository"):
        raise RuntimeError(
            "Project Truth targetRepository does not match the verified Unity target repository."
        )
    if str(project_truth.get("targetCommit") or "").lower() != str(unity.get("targetCommit") or "").lower():
        raise RuntimeError(
            "Project Truth targetCommit does not match the verified Unity target commit."
        )

    claim = project_truth.get("claim")
    truth = project_truth.get("projectTruth")
    ingestion = project_truth.get("collectorIngestion")
    if not isinstance(ingestion, dict):
        raise RuntimeError("Project Truth evidence does not contain collector ingestion output.")
    if not isinstance(claim, dict):
        raise RuntimeError("Project Truth evidence does not contain the completion claim output.")
    if not isinstance(truth, dict):
        raise RuntimeError("Project Truth evidence does not contain dashboard output.")

    if truth.get("qualityGateStatus") != "Passed":
        raise RuntimeError(
            f"Project Truth quality gate is not Passed: {truth.get('qualityGateStatus')!r}"
        )
    if claim.get("verificationStatus") != "Confirmed":
        raise RuntimeError(
            "The real-project completion claim is not Confirmed by current-revision evidence: "
            f"{claim.get('verificationStatus')!r}"
        )
    if int(truth.get("contradictionCount") or 0) != 0:
        raise RuntimeError(
            f"Project Truth contains contradictions: {truth.get('contradictionCount')!r}"
        )

    output_root = Path(args.output_root).resolve() / expected
    output_root.mkdir(parents=True, exist_ok=True)

    copied: list[dict] = []
    for label, source in paths.items():
        destination = output_root / source.name
        if source.resolve() != destination.resolve():
            destination.write_bytes(source.read_bytes())
        copied.append(
            {
                "kind": label,
                "file": destination.name,
                "sha256": sha256(destination),
                "bytes": destination.stat().st_size,
            }
        )

    summary = {
        "schema": "dip.final-portfolio-evidence.v2",
        "status": "PASS",
        "sourceCommit": expected,
        "requiredCi": "11/11 Green",
        "benchmarkPassed": True,
        "unityRuntimeVerified": True,
        "unityQualityGatePassed": True,
        "unityTarget": {
            "repository": unity.get("targetRepository"),
            "commit": unity.get("targetCommit"),
            "engineVersion": unity.get("unityVersion"),
        },
        "unrealRuntimeVerified": True,
        "unrealQualityGatePassed": True,
        "unrealTarget": {
            "repository": unreal.get("targetRepository"),
            "commit": unreal.get("targetCommit"),
            "project": unreal.get("targetProject"),
        },
        "projectTruthCaptured": True,
        "projectTruthQualityGate": truth.get("qualityGateStatus"),
        "projectTruthClaimVerification": claim.get("verificationStatus"),
        "projectTruthContradictionCount": truth.get("contradictionCount"),
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "files": copied,
    }
    summary_path = output_root / "FINAL_EVIDENCE_SUMMARY.json"
    summary_path.write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    manifest_entries = copied + [
        {
            "kind": "summary",
            "file": summary_path.name,
            "sha256": sha256(summary_path),
            "bytes": summary_path.stat().st_size,
        }
    ]
    manifest_path = output_root / "SHA256SUMS.txt"
    manifest_path.write_text(
        "".join(f"{entry['sha256']}  {entry['file']}\n" for entry in manifest_entries),
        encoding="ascii",
    )

    print("FINAL PORTFOLIO EVIDENCE: PASS")
    print(f"Exact DIP SHA: {expected}")
    print("Required CI: 11/11 Green")
    print("Benchmark: exact revision PASS")
    print("Unity: runtime verified + quality gate PASS")
    print("Unreal: runtime verified + quality gate PASS")
    print("Project Truth: quality gate Passed + completion claim Confirmed + contradictions 0")
    print(f"Summary: {summary_path}")
    print(f"Manifest: {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
