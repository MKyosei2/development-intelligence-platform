# Purpose: Verifies final portfolio evidence tooling fails closed on revision mismatches, missing evidence, and non-green verification states.
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
    unity = write_json(
        tmp_path / "unity.json",
        {
            "dipSourceCommit": SHA,
            "runtimeVerified": True,
            "qualityGatePassed": True,
            "targetRepository": "https://example.invalid/game",
            "targetCommit": "c" * 40,
            "unityVersion": "test",
        },
    )
    unreal = write_json(
        tmp_path / "unreal.json",
        {
            "dipSourceCommit": SHA,
            "runtimeVerified": True,
            "qualityGatePassed": True,
            "targetRepository": "https://example.invalid/unreal",
            "targetCommit": "d" * 40,
            "targetProject": "TestProject",
            "engineNativeAutomation": {
                "attempted": True,
                "timedOut": False,
                "report": "report.json",
            },
        },
    )
    project_truth = write_json(
        tmp_path / "project-truth.json",
        {
            "dipSourceCommit": SHA,
            "targetRepository": "https://example.invalid/game",
            "targetCommit": "c" * 40,
            "collectorIngestion": {"accepted": True},
            "claim": {"verificationStatus": "Confirmed"},
            "projectTruth": {
                "qualityGateStatus": "Passed",
                "contradictionCount": 0,
            },
        },
    )
    return {
        "ci": ci,
        "benchmark": benchmark,
        "unity": unity,
        "unreal": unreal,
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
        "--unity",
        str(files["unity"]),
        "--unreal",
        str(files["unreal"]),
        "--project-truth",
        str(files["project_truth"]),
        "--output-root",
        str(output),
    ]


def test_finalizer_accepts_only_complete_same_revision_bundle(tmp_path: Path) -> None:
    files = valid_final_evidence(tmp_path)
    result = run_tool(
        "finalize_portfolio_evidence.py",
        *finalize_args(files, tmp_path / "out"),
    )

    assert result.returncode == 0, result.stderr
    summary = tmp_path / "out" / SHA / "FINAL_EVIDENCE_SUMMARY.json"
    payload = json.loads(summary.read_text(encoding="utf-8"))
    assert payload["status"] == "PASS"
    assert payload["sourceCommit"] == SHA


@pytest.mark.parametrize(
    ("file_key", "field", "bad_value", "message"),
    [
        ("ci", "sourceCommit", OTHER_SHA, "CI matrix is not bound"),
        ("benchmark", "sourceCommit", OTHER_SHA, "Benchmark is not bound"),
        ("unity", "dipSourceCommit", OTHER_SHA, "Unity evidence is not bound"),
        ("unreal", "dipSourceCommit", OTHER_SHA, "Unreal evidence is not bound"),
        ("project_truth", "dipSourceCommit", OTHER_SHA, "Project Truth case-study evidence is not bound"),
    ],
)
def test_finalizer_rejects_revision_mismatch(
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

    result = run_tool(
        "finalize_portfolio_evidence.py",
        *finalize_args(files, tmp_path / "out"),
    )

    assert result.returncode != 0
    assert message in (result.stderr + result.stdout)


@pytest.mark.parametrize(
    ("file_key", "field", "bad_value", "message"),
    [
        ("ci", "allGreen", False, "11/11 Green"),
        ("benchmark", "sourceWorkingTreeDirty", True, "dirty working tree"),
        ("benchmark", "passed", False, "Benchmark passed must be true"),
        ("unity", "runtimeVerified", False, "Unity runtimeVerified must be true"),
        ("unity", "qualityGatePassed", False, "Unity qualityGatePassed must be true"),
        ("unreal", "runtimeVerified", False, "Unreal runtimeVerified must be true"),
        ("unreal", "qualityGatePassed", False, "Unreal qualityGatePassed must be true"),
    ],
)
def test_finalizer_rejects_non_verified_states(
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

    result = run_tool(
        "finalize_portfolio_evidence.py",
        *finalize_args(files, tmp_path / "out"),
    )

    assert result.returncode != 0
    assert message in (result.stderr + result.stdout)


def test_finalizer_rejects_unconfirmed_project_truth(tmp_path: Path) -> None:
    files = valid_final_evidence(tmp_path)
    payload = json.loads(files["project_truth"].read_text(encoding="utf-8"))
    payload["claim"]["verificationStatus"] = "Unverified"
    write_json(files["project_truth"], payload)

    result = run_tool(
        "finalize_portfolio_evidence.py",
        *finalize_args(files, tmp_path / "out"),
    )

    assert result.returncode != 0
    assert "completion claim is not Confirmed" in (result.stderr + result.stdout)


def test_finalizer_rejects_project_truth_contradictions(tmp_path: Path) -> None:
    files = valid_final_evidence(tmp_path)
    payload = json.loads(files["project_truth"].read_text(encoding="utf-8"))
    payload["projectTruth"]["contradictionCount"] = 1
    write_json(files["project_truth"], payload)

    result = run_tool(
        "finalize_portfolio_evidence.py",
        *finalize_args(files, tmp_path / "out"),
    )

    assert result.returncode != 0
    assert "contains contradictions" in (result.stderr + result.stdout)


def test_publish_real_project_evidence_rejects_wrong_dip_revision_before_network(
    tmp_path: Path,
) -> None:
    evidence = write_json(
        tmp_path / "engine.json",
        {
            "dipSourceCommit": OTHER_SHA,
            "runtimeVerified": True,
            "qualityGatePassed": True,
            "targetRepository": "https://example.invalid/game",
            "targetCommit": "c" * 40,
        },
    )
    report = write_json(
        tmp_path / "report.json",
        {
            "commitSha": "c" * 40,
            "status": "Succeeded",
        },
    )

    result = run_tool(
        "publish_real_project_evidence.py",
        "--engine-evidence",
        str(evidence),
        "--report",
        str(report),
        "--expected-dip-sha",
        SHA,
        "--api-base",
        "http://127.0.0.1:1",
        "--output",
        str(tmp_path / "out"),
    )

    assert result.returncode != 0
    assert "not bound to the expected DIP SHA" in (result.stderr + result.stdout)


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
    assert len(module.REQUIRED) == 11


def test_unity_verifier_rejects_expected_sha_mismatch(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    module = load_module("verify_unity_real_project", "verify_unity_real_project.py")

    def fake_git_text(root: Path, *args: str) -> str:
        if args == ("rev-parse", "HEAD"):
            return SHA
        if args == ("status", "--porcelain", "--untracked-files=no"):
            return ""
        if args == ("branch", "--show-current"):
            return "main"
        raise AssertionError(args)

    monkeypatch.setattr(module, "git_text", fake_git_text)

    with pytest.raises(RuntimeError, match="DIP SHA mismatch"):
        module.ensure_clean_dip(tmp_path, OTHER_SHA)


def test_unity_verifier_rejects_dirty_tracked_tree(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    module = load_module("verify_unity_real_project_dirty", "verify_unity_real_project.py")

    def fake_git_text(root: Path, *args: str) -> str:
        if args == ("rev-parse", "HEAD"):
            return SHA
        if args == ("status", "--porcelain", "--untracked-files=no"):
            return " M src/file.cs"
        if args == ("branch", "--show-current"):
            return "main"
        raise AssertionError(args)

    monkeypatch.setattr(module, "git_text", fake_git_text)

    with pytest.raises(RuntimeError, match="clean tracked DIP working tree"):
        module.ensure_clean_dip(tmp_path, SHA)


def test_unreal_verifier_rejects_expected_sha_mismatch(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    module = load_module("verify_unreal_real_project", "verify_unreal_real_project.py")

    def fake_git_text(root: Path, *args: str) -> str:
        if args == ("rev-parse", "HEAD"):
            return SHA
        if args == ("status", "--porcelain", "--untracked-files=no"):
            return ""
        if args == ("branch", "--show-current"):
            return "main"
        raise AssertionError(args)

    monkeypatch.setattr(module, "git_text", fake_git_text)

    with pytest.raises(RuntimeError, match="DIP SHA mismatch"):
        module.ensure_clean_dip(tmp_path, OTHER_SHA)
