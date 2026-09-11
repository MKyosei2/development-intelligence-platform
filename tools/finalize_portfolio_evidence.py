#!/usr/bin/env python3
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
        raise RuntimeError(f"{label} is not bound to the final DIP SHA. expected={expected} actual={value}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Fail closed unless final CI, benchmark, Unity and Unreal evidence all belong to one exact DIP SHA.")
    parser.add_argument("--sha", required=True)
    parser.add_argument("--ci", default="artifacts/final-evidence/ci-matrix.json")
    parser.add_argument("--benchmark", default="artifacts/portfolio-benchmark/portfolio-benchmark-results.json")
    parser.add_argument("--unity", default="artifacts/real-project-runtime/unity/unity-real-project-evidence.json")
    parser.add_argument("--unreal", default="artifacts/real-project-runtime/unreal/unreal-real-project-evidence.json")
    parser.add_argument("--project-truth")
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
    }
    if args.project_truth:
        paths["projectTruth"] = Path(args.project_truth).resolve()

    ci = load(paths["ci"])
    benchmark = load(paths["benchmark"])
    unity = load(paths["unity"])
    unreal = load(paths["unreal"])

    require_sha("CI matrix", ci.get("sourceCommit"), expected)
    if ci.get("requiredCount") != 11 or ci.get("greenCount") != 11 or ci.get("allGreen") is not True:
        raise RuntimeError("Final CI evidence is not an 11/11 Green matrix.")

    require_sha("Benchmark", benchmark.get("sourceCommit"), expected)
    if benchmark.get("sourceWorkingTreeDirty") is not False:
        raise RuntimeError("Benchmark was measured from a dirty working tree.")
    if benchmark.get("passed") is not True:
        raise RuntimeError("Benchmark gates are not all PASS.")

    require_sha("Unity evidence", unity.get("dipSourceCommit"), expected)
    if unity.get("runtimeVerified") is not True:
        raise RuntimeError("Unity evidence does not prove actual engine runtime verification.")
    if not unity.get("targetRepository") or not unity.get("targetCommit"):
        raise RuntimeError("Unity target project identity is incomplete.")

    require_sha("Unreal evidence", unreal.get("dipSourceCommit"), expected)
    if unreal.get("runtimeVerified") is not True:
        raise RuntimeError("Unreal evidence does not prove actual UnrealEditor-Cmd execution.")
    engine = unreal.get("engineNativeAutomation") or {}
    if engine.get("attempted") is not True or engine.get("timedOut") is True or not engine.get("report"):
        raise RuntimeError("Unreal native Automation execution evidence is incomplete.")

    project_truth = None
    if "projectTruth" in paths:
        project_truth = load(paths["projectTruth"])
        require_sha("Project Truth case-study evidence", project_truth.get("dipSourceCommit"), expected)
        if not project_truth.get("projectTruth"):
            raise RuntimeError("Project Truth case-study evidence does not contain dashboard output.")

    output_root = Path(args.output_root).resolve() / expected
    output_root.mkdir(parents=True, exist_ok=True)

    copied: list[dict] = []
    for label, source in paths.items():
        destination = output_root / source.name
        if source.resolve() != destination.resolve():
            destination.write_bytes(source.read_bytes())
        copied.append({
            "kind": label,
            "file": destination.name,
            "sha256": sha256(destination),
            "bytes": destination.stat().st_size,
        })

    summary = {
        "schema": "dip.final-portfolio-evidence.v1",
        "status": "PASS",
        "sourceCommit": expected,
        "requiredCi": "11/11 Green",
        "benchmarkPassed": True,
        "unityRuntimeVerified": True,
        "unityTarget": {
            "repository": unity.get("targetRepository"),
            "commit": unity.get("targetCommit"),
            "engineVersion": unity.get("unityVersion"),
            "qualityGatePassed": unity.get("qualityGatePassed"),
        },
        "unrealRuntimeVerified": True,
        "unrealTarget": {
            "repository": unreal.get("targetRepository"),
            "commit": unreal.get("targetCommit"),
            "project": unreal.get("targetProject"),
            "qualityGatePassed": unreal.get("qualityGatePassed"),
        },
        "projectTruthCaptured": project_truth is not None,
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "files": copied,
    }
    summary_path = output_root / "FINAL_EVIDENCE_SUMMARY.json"
    summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    manifest_entries = copied + [{
        "kind": "summary",
        "file": summary_path.name,
        "sha256": sha256(summary_path),
        "bytes": summary_path.stat().st_size,
    }]
    manifest_path = output_root / "SHA256SUMS.txt"
    manifest_path.write_text(
        "".join(f"{entry['sha256']}  {entry['file']}\n" for entry in manifest_entries),
        encoding="ascii",
    )

    print("FINAL PORTFOLIO EVIDENCE: PASS")
    print(f"Exact DIP SHA: {expected}")
    print("Required CI: 11/11 Green")
    print("Benchmark: exact revision PASS")
    print("Unity: real engine runtime verified")
    print("Unreal: real engine runtime verified")
    print(f"Summary: {summary_path}")
    print(f"Manifest: {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
