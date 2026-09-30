# Purpose: Verifies final portfolio evidence tooling fails closed on revision mismatch, dirty measurements, incomplete QA evidence, and incorrect Project Truth outcomes.
from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / "tools"
SHA = "a" * 40
OTHER_SHA = "b" * 40


def load_module(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, TOOLS / filename)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def run_tool(filename: str, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(TOOLS / filename), *args],
        cwd=ROOT,
        text=True,
        capture_output=True,
        check=False,
    )


def write_json(path: Path, value: object) -> Path:
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


def valid_final_evidence(tmp_path: Path) -> dict[str, Path]:
    ci = write_json(
        tmp_path / "ci.json",
        {
            "sourceCommit": SHA,
            "requiredCount": 11,
            "greenCount": 11,
            "allGreen": True,
        },
    )
    benchmark = write_json(
        tmp_path / "benchmark.json",
        {
            "sourceCommit": SHA,
            "sourceWorkingTreeDirty": False,
            "passed": True,
        },
    )
    qa = write_json(
        tmp_path / "qa-bug-zoo.json",
        {
            "sourceCommit": SHA,
            "sourceWorkingTreeDirty": False,
            "passed": True,
            "blockingInfrastructureFailures": 0,
            "missingRequiredLanguages": [],
            "detectionRecallPercent": 100.0,
            "falsePositiveRatePercent": 0.0,
            "reproductionRatePercent": 100.0,
            "executedLanguages": ["CSharp", "Cpp", "Python"],
        },
    )
    project_truth = write_json(
        tmp_path / "project-truth.json",
        {
            "status": "PASS",
            "dipSourceCommit": SHA,
            "claim": {"verificationStatus": "Contradicted"},
            "projectTruth": {
                "qualityGateStatus": "Failed",
                "reportedProgress": 100.0,
                "verifiedProgress": 0.0,
                "contradictionCount": 1,
            },
            "assertions": {
                "qualityGateFailed": True,
                "completionClaimContradicted": True,
                "verifiedProgressBelowReported": True,
                "contradictionRecorded": True,
            },
        },
    )
    return {
        "ci": ci,
        "benchmark": benchmark,
        "qa": qa,
        "project_truth": project_truth,
    }


def finalize_args(files: dict[str, Path], output: Path) -> list[str]:
    return [
        "--sha",
        SHA,
        "--ci",
        str(files["ci"]),
        "--benchmark",
        str(files["benchmark"]),
        "--qa-bug-zoo",
        str(files["qa"]),
        "--project-truth",
        str(files["project_truth"]),
        "--output-root",
        str(output),
    ]


def test_finalizer_accepts_complete_same_revision_bundle(tmp_path: Path) -> None:
    files = valid_final_evidence(tmp_path)
    result = run_tool("finalize_portfolio_evidence.py", *finalize_args(files, tmp_path / "out"))
    assert result.returncode == 0, result.stderr

    summary = tmp_path / "out" / SHA / "FINAL_EVIDENCE_SUMMARY.json"
    payload = json.loads(summary.read_text(encoding="utf-8"))
    assert payload["status"] == "PASS"
    assert payload["sourceCommit"] == SHA
    assert payload["engineRuntime"]["unity"] == "NOT_EVALUATED"
    assert payload["engineRuntime"]["unreal"] == "NOT_EVALUATED"


@pytest.mark.parametrize(
    ("file_key", "field", "message"),
    [
        ("ci", "sourceCommit", "CI matrix is not bound"),
        ("benchmark", "sourceCommit", "Benchmark is not bound"),
        ("qa", "sourceCommit", "QA Bug Zoo is not bound"),
        ("project_truth", "dipSourceCommit", "Project Truth QA scenario is not bound"),
    ],
)
def test_finalizer_rejects_revision_mismatch(
    tmp_path: Path,
    file_key: str,
    field: str,
    message: str,
) -> None:
    files = valid_final_evidence(tmp_path)
    payload = json.loads(files[file_key].read_text(encoding="utf-8"))
    payload[field] = OTHER_SHA
    write_json(files[file_key], payload)

    result = run_tool("finalize_portfolio_evidence.py", *finalize_args(files, tmp_path / "out"))
    assert result.returncode != 0
    assert message in (result.stderr + result.stdout)


def test_finalizer_rejects_incomplete_ci_matrix(tmp_path: Path) -> None:
    files = valid_final_evidence(tmp_path)
    payload = json.loads(files["ci"].read_text(encoding="utf-8"))
    payload["greenCount"] = 10
    payload["allGreen"] = False
    write_json(files["ci"], payload)

    result = run_tool("finalize_portfolio_evidence.py", *finalize_args(files, tmp_path / "out"))
    assert result.returncode != 0
    assert "not fully Green" in (result.stderr + result.stdout)


@pytest.mark.parametrize(
    ("file_key", "field", "bad_value", "message"),
    [
        ("benchmark", "sourceWorkingTreeDirty", True, "dirty working tree"),
        ("benchmark", "passed", False, "Benchmark passed must be true"),
        ("qa", "sourceWorkingTreeDirty", True, "dirty working tree"),
        ("qa", "passed", False, "QA Bug Zoo passed must be true"),
        ("qa", "blockingInfrastructureFailures", 1, "blocking infrastructure failures"),
        ("qa", "missingRequiredLanguages", ["Cpp"], "missing required languages"),
    ],
)
def test_finalizer_rejects_non_verified_measurement_states(
    tmp_path: Path,
    file_key: str,
    field: str,
    bad_value: object,
    message: str,
) -> None:
    files = valid_final_evidence(tmp_path)
    payload = json.loads(files[file_key].read_text(encoding="utf-8"))
    payload[field] = bad_value
    write_json(files[file_key], payload)

    result = run_tool("finalize_portfolio_evidence.py", *finalize_args(files, tmp_path / "out"))
    assert result.returncode != 0
    assert message.lower() in (result.stderr + result.stdout).lower()


@pytest.mark.parametrize(
    ("mutation", "message"),
    [
        (lambda value: value["assertions"].__setitem__("qualityGateFailed", False), "qualityGateFailed"),
        (lambda value: value["projectTruth"].__setitem__("qualityGateStatus", "Passed"), "fail its quality gate"),
        (lambda value: value["claim"].__setitem__("verificationStatus", "Confirmed"), "contradicted"),
        (lambda value: value["projectTruth"].__setitem__("verifiedProgress", 100.0), "verified progress below"),
        (lambda value: value["projectTruth"].__setitem__("contradictionCount", 0), "at least one contradiction"),
    ],
)
def test_finalizer_rejects_wrong_project_truth_scenario(
    tmp_path: Path,
    mutation,
    message: str,
) -> None:
    files = valid_final_evidence(tmp_path)
    payload = json.loads(files["project_truth"].read_text(encoding="utf-8"))
    mutation(payload)
    write_json(files["project_truth"], payload)

    result = run_tool("finalize_portfolio_evidence.py", *finalize_args(files, tmp_path / "out"))
    assert result.returncode != 0
    assert message.lower() in (result.stderr + result.stdout).lower()


def test_ci_matrix_latest_for_name_uses_exact_sha_and_latest_attempt() -> None:
    module = load_module("verify_final_ci_matrix", "verify_final_ci_matrix.py")
    runs = [
        {
            "name": ".NET CI",
            "head_sha": SHA,
            "run_number": 9,
            "run_attempt": 1,
            "created_at": "2026-01-01T00:00:00Z",
        },
        {
            "name": ".NET CI",
            "head_sha": SHA,
            "run_number": 9,
            "run_attempt": 2,
            "created_at": "2026-01-01T00:01:00Z",
        },
        {
            "name": ".NET CI",
            "head_sha": OTHER_SHA,
            "run_number": 10,
            "run_attempt": 1,
            "created_at": "2026-01-01T00:02:00Z",
        },
    ]
    selected = module.latest_for_name(runs, ".NET CI", SHA)
    assert selected is not None
    assert selected["head_sha"] == SHA
    assert selected["run_attempt"] == 2
    assert "Unreal Collector Contract CI" in module.REQUIRED
    assert "Unreal Collector CI" not in module.REQUIRED
