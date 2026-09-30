#!/usr/bin/env python3
# Purpose: Finalizes exact-revision CI, benchmark, QA Bug Zoo, and Project Truth scenario evidence into one auditable bundle.
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
    actual = str(value or "").lower()
    if actual != expected.lower():
        raise RuntimeError(
            f"{label} is not bound to the final DIP SHA. expected={expected} actual={value}"
        )


def require_true(label: str, value: object) -> None:
    if value is not True:
        raise RuntimeError(f"{label} must be true for final publication evidence; actual={value!r}")


def require_project_truth_scenario(project_truth: dict) -> None:
    if project_truth.get("status") != "PASS":
        raise RuntimeError(f"Project Truth scenario status is not PASS: {project_truth.get('status')!r}")

    assertions = project_truth.get("assertions")
    if not isinstance(assertions, dict):
        raise RuntimeError("Project Truth scenario does not contain assertions.")

    for key in (
        "qualityGateFailed",
        "completionClaimContradicted",
        "verifiedProgressBelowReported",
        "contradictionRecorded",
    ):
        require_true(f"Project Truth assertion {key}", assertions.get(key))

    truth = project_truth.get("projectTruth")
    claim = project_truth.get("claim")
    if not isinstance(truth, dict) or not isinstance(claim, dict):
        raise RuntimeError("Project Truth scenario must contain projectTruth and claim objects.")

    if truth.get("qualityGateStatus") != "Failed":
        raise RuntimeError(
            f"Expected the intentional QA scenario to fail its quality gate; actual={truth.get('qualityGateStatus')!r}"
        )
    if claim.get("verificationStatus") != "Contradicted":
        raise RuntimeError(
            "Expected the 100% Complete claim to be contradicted by QA evidence; "
            f"actual={claim.get('verificationStatus')!r}"
        )

    reported = truth.get("reportedProgress")
    verified = truth.get("verifiedProgress")
    if not isinstance(reported, (int, float)) or not isinstance(verified, (int, float)):
        raise RuntimeError("Project Truth scenario is missing numeric reported/verified progress.")
    if verified >= reported:
        raise RuntimeError(
            f"Expected verified progress below reported progress; reported={reported} verified={verified}"
        )
    if int(truth.get("contradictionCount") or 0) < 1:
        raise RuntimeError("Expected at least one contradiction in the intentional QA scenario.")


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Fail closed unless final CI, benchmark, QA Bug Zoo, and Project Truth scenario evidence "
            "all belong to one exact DIP SHA. Unity/Unreal engine runtime is not a final-publication requirement."
        )
    )
    parser.add_argument("--sha", required=True)
    parser.add_argument("--ci", default="artifacts/final-evidence/ci-matrix.json")
    parser.add_argument("--benchmark", default="artifacts/portfolio-benchmark/portfolio-benchmark-results.json")
    parser.add_argument("--qa-bug-zoo", default="artifacts/qa-bug-zoo/qa-bug-zoo-results.json")
    parser.add_argument(
        "--project-truth",
        required=True,
        help="Expected-failure QA scenario evidence that demonstrates Project Truth contradiction handling.",
    )
    parser.add_argument("--output-root", default="artifacts/final-evidence")
    args = parser.parse_args()

    expected = args.sha.lower()
    if len(expected) != 40 or any(ch not in "0123456789abcdef" for ch in expected):
        raise RuntimeError(f"Invalid final SHA: {args.sha!r}")

    paths = {
        "ci": Path(args.ci).resolve(),
        "benchmark": Path(args.benchmark).resolve(),
        "qaBugZoo": Path(args.qa_bug_zoo).resolve(),
        "projectTruth": Path(args.project_truth).resolve(),
    }

    ci = load(paths["ci"])
    benchmark = load(paths["benchmark"])
    qa = load(paths["qaBugZoo"])
    project_truth = load(paths["projectTruth"])

    require_sha("CI matrix", ci.get("sourceCommit"), expected)
    required_count = int(ci.get("requiredCount") or 0)
    green_count = int(ci.get("greenCount") or 0)
    if required_count <= 0 or green_count != required_count or ci.get("allGreen") is not True:
        raise RuntimeError(
            f"Final CI evidence is not fully Green. required={required_count} green={green_count} allGreen={ci.get('allGreen')!r}"
        )

    require_sha("Benchmark", benchmark.get("sourceCommit"), expected)
    if benchmark.get("sourceWorkingTreeDirty") is not False:
        raise RuntimeError("Benchmark was measured from a dirty working tree.")
    require_true("Benchmark passed", benchmark.get("passed"))

    require_sha("QA Bug Zoo", qa.get("sourceCommit"), expected)
    if qa.get("sourceWorkingTreeDirty") is not False:
        raise RuntimeError("QA Bug Zoo was measured from a dirty working tree.")
    require_true("QA Bug Zoo passed", qa.get("passed"))
    if int(qa.get("blockingInfrastructureFailures") or 0) != 0:
        raise RuntimeError(
            "QA Bug Zoo contains blocking infrastructure failures: "
            f"{qa.get('blockingInfrastructureFailures')!r}"
        )
    missing_required = qa.get("missingRequiredLanguages") or []
    if not isinstance(missing_required, list) or missing_required:
        raise RuntimeError(f"QA Bug Zoo is missing required languages: {missing_required!r}")

    require_sha("Project Truth QA scenario", project_truth.get("dipSourceCommit"), expected)
    require_project_truth_scenario(project_truth)

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

    truth = project_truth["projectTruth"]
    claim = project_truth["claim"]
    summary = {
        "schema": "dip.final-portfolio-evidence.v3",
        "status": "PASS",
        "sourceCommit": expected,
        "requiredCi": f"{green_count}/{required_count} Green",
        "benchmarkPassed": True,
        "qaBugZooPassed": True,
        "qaBugZoo": {
            "detectionRecallPercent": qa.get("detectionRecallPercent"),
            "falsePositiveRatePercent": qa.get("falsePositiveRatePercent"),
            "reproductionRatePercent": qa.get("reproductionRatePercent"),
            "executedLanguages": qa.get("executedLanguages"),
        },
        "projectTruthScenarioPassed": True,
        "projectTruthExpectedOutcome": {
            "qualityGateStatus": truth.get("qualityGateStatus"),
            "claimVerificationStatus": claim.get("verificationStatus"),
            "reportedProgress": truth.get("reportedProgress"),
            "verifiedProgress": truth.get("verifiedProgress"),
            "contradictionCount": truth.get("contradictionCount"),
        },
        "engineRuntime": {
            "unity": "NOT_EVALUATED",
            "unreal": "NOT_EVALUATED",
            "note": "Engine-specific collectors may exist, but final portfolio publication does not claim runtime verification without a real target project.",
        },
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "files": copied,
    }

    summary_path = output_root / "FINAL_EVIDENCE_SUMMARY.json"
    summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

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
    print(f"Required CI: {green_count}/{required_count} Green")
    print("Benchmark: exact revision PASS")
    print("QA Bug Zoo: exact revision PASS")
    print("Project Truth: intentional Critical QA evidence correctly contradicted a Complete claim")
    print("Unity runtime: NOT_EVALUATED (not required for publication)")
    print("Unreal runtime: NOT_EVALUATED (not required for publication)")
    print(f"Summary: {summary_path}")
    print(f"Manifest: {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
