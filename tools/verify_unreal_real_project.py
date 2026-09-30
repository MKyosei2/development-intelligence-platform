#!/usr/bin/env python3
# Purpose: Optionally verifies the DIP Unreal collector against an explicitly supplied real Git-backed Unreal project; not required for portfolio publication.
from __future__ import annotations

import argparse
import json
import os
import subprocess
import uuid
from datetime import datetime, timezone
from pathlib import Path

PLACEHOLDER_PROJECT_ID = "22222222-2222-2222-2222-222222222222"


def run(cmd: list[str], cwd: Path | None = None, check: bool = True) -> subprocess.CompletedProcess[str]:
    print(">", subprocess.list2cmdline(cmd))
    cp = subprocess.run(cmd, cwd=cwd, text=True, capture_output=True, check=False)
    if cp.stdout:
        print(cp.stdout.rstrip())
    if cp.stderr:
        print(cp.stderr.rstrip())
    if check and cp.returncode != 0:
        raise RuntimeError(f"Command failed with exit code {cp.returncode}: {subprocess.list2cmdline(cmd)}")
    return cp


def git_text(root: Path, *args: str) -> str:
    return run(["git", "-C", str(root), *args]).stdout.strip()


def ensure_clean_dip(root: Path, expected_sha: str | None) -> tuple[str, str]:
    sha = git_text(root, "rev-parse", "HEAD")
    if len(sha) != 40:
        raise RuntimeError("Could not resolve a 40-character DIP source SHA.")
    if expected_sha and sha.lower() != expected_sha.lower():
        raise RuntimeError(f"DIP SHA mismatch: expected={expected_sha} actual={sha}")
    dirty = git_text(root, "status", "--porcelain", "--untracked-files=no")
    if dirty:
        raise RuntimeError("Optional Unreal runtime verification requires a clean tracked DIP working tree.\n" + dirty)
    branch = git_text(root, "branch", "--show-current") or "detached"
    return sha, branch


def require_project(explicit: str) -> tuple[Path, Path]:
    path = Path(explicit).expanduser().resolve()
    if path.is_file() and path.suffix.lower() == ".uproject":
        project, uproject = path.parent, path
    elif path.is_dir():
        candidates = list(path.glob("*.uproject"))
        if len(candidates) != 1:
            raise RuntimeError(f"Expected exactly one .uproject at {path}; found {len(candidates)}.")
        project, uproject = path, candidates[0]
    else:
        raise RuntimeError(f"Expected a .uproject file or project directory: {path}")
    if not (project / ".git").exists():
        raise RuntimeError("Optional Unreal runtime evidence requires a Git-backed target project.")
    return project, uproject


def require_editor(explicit: str) -> Path:
    path = Path(explicit).expanduser().resolve()
    if not path.is_file():
        raise RuntimeError(f"UnrealEditor-Cmd does not exist: {path}")
    return path


def target_identity(project: Path) -> tuple[str, str, str]:
    sha = git_text(project, "rev-parse", "HEAD")
    branch = git_text(project, "branch", "--show-current") or "detached"
    remote = run(["git", "-C", str(project), "remote", "get-url", "origin"], check=False).stdout.strip()
    if not remote:
        remote = f"file://{project.as_posix()}"
    if remote.endswith(".git"):
        remote = remote[:-4]
    return sha, branch, remote


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "OPTIONAL: run the DIP Unreal collector through UnrealEditor-Cmd against an explicitly supplied real Git-backed project. "
            "This tool is not part of the final portfolio publication gate."
        )
    )
    parser.add_argument("--dip-root", default=str(Path(__file__).resolve().parents[1]))
    parser.add_argument("--project", required=True)
    parser.add_argument("--unreal-editor-cmd", required=True)
    parser.add_argument("--expected-dip-sha")
    parser.add_argument("--automation-filter", default="Project")
    parser.add_argument("--output", default="artifacts/optional-engine-runtime/unreal")
    parser.add_argument("--require-quality-pass", action="store_true")
    args = parser.parse_args()

    dip_root = Path(args.dip_root).expanduser().resolve()
    dip_sha, dip_branch = ensure_clean_dip(dip_root, args.expected_dip_sha)
    project, uproject = require_project(args.project)
    editor = require_editor(args.unreal_editor_cmd)
    target_sha, target_branch, target_repository = target_identity(project)

    output = (dip_root / args.output).resolve() if not Path(args.output).is_absolute() else Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    report_path = output / "unreal-optional-runtime-report.json"
    evidence_path = output / "unreal-optional-runtime-evidence.json"
    external_run_id = f"unreal-optional-{dip_sha[:12]}-{uuid.uuid4().hex}"

    collector = dip_root / "collectors" / "unreal-collector" / "collector.py"
    command = [
        os.fspath(Path(os.sys.executable).resolve()), str(collector),
        "--root", str(project),
        "--project-id", PLACEHOLDER_PROJECT_ID,
        "--repository", target_repository,
        "--commit-sha", target_sha,
        "--branch", target_branch,
        "--external-run-id", external_run_id,
        "--attempt-number", "1",
        "--unreal-editor-cmd", str(editor),
        "--automation-filter", args.automation_filter,
        "--quality-gate", "critical",
        "--output", str(report_path),
    ]
    completed = run(command, cwd=dip_root, check=False)
    if completed.returncode not in {0, 2}:
        raise RuntimeError(f"Unreal collector failed with exit code {completed.returncode}.")
    if not report_path.is_file():
        raise RuntimeError("Unreal collector did not produce a report.")

    report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    for key, expected in {
        "externalRunId": external_run_id,
        "attemptNumber": 1,
        "repository": target_repository,
        "commitSha": target_sha,
        "branch": target_branch,
    }.items():
        if report.get(key) != expected:
            raise RuntimeError(f"Unreal report identity mismatch for {key}: expected={expected!r} actual={report.get(key)!r}")

    engine = report.get("engineNativeAutomation") or {}
    runtime_verified = bool(engine.get("attempted")) and not bool(engine.get("timedOut")) and bool(engine.get("report"))
    if not runtime_verified:
        raise RuntimeError("UnrealEditor-Cmd engine-native automation was not proven complete: " + json.dumps(engine, ensure_ascii=False))

    quality_passed = completed.returncode == 0 and report.get("status") == "Succeeded"
    if args.require_quality_pass and not quality_passed:
        raise RuntimeError("Unreal runtime was verified, but the critical quality gate did not pass.")

    evidence = {
        "schema": "dip.optional-engine.unreal.v1",
        "status": "RuntimeVerified",
        "publicationRequirement": False,
        "runtimeVerified": True,
        "qualityGatePassed": quality_passed,
        "dipSourceCommit": dip_sha,
        "dipSourceBranch": dip_branch,
        "targetProject": uproject.stem,
        "targetRepository": target_repository,
        "targetCommit": target_sha,
        "targetBranch": target_branch,
        "unrealProject": str(uproject),
        "unrealEditorCmd": str(editor),
        "automationFilter": args.automation_filter,
        "engineNativeAutomation": engine,
        "collectorExitCode": completed.returncode,
        "externalRunId": external_run_id,
        "report": str(report_path),
        "verifiedAtUtc": datetime.now(timezone.utc).isoformat(),
        "note": "Optional runtime evidence only. Absence of this artifact does not block final portfolio publication.",
    }
    evidence_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"OPTIONAL UNREAL RUNTIME VERIFIED: DIP {dip_sha} against {target_repository}@{target_sha}")
    print(f"Evidence: {evidence_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
