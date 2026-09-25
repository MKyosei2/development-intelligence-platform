#!/usr/bin/env python3
# Purpose: Defines verify unreal real project responsibilities for this repository area.
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
        raise RuntimeError("Real Unreal verification requires a clean tracked DIP working tree.\n" + dirty)
    branch = git_text(root, "branch", "--show-current") or "detached"
    return sha, branch


def find_project(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit).expanduser().resolve()
        if path.is_file() and path.suffix.lower() == ".uproject":
            return path.parent
        if path.is_dir() and len(list(path.glob("*.uproject"))) == 1:
            return path
        raise RuntimeError(f"Expected a .uproject file or a directory containing exactly one .uproject: {path}")

    roots = [
        Path.home() / "Documents" / "Unreal Projects",
        Path.home() / "Documents" / "GitHub",
        Path.home() / "source" / "repos",
    ]
    candidates: list[Path] = []
    for root in roots:
        if not root.is_dir():
            continue
        for uproject in root.glob("**/*.uproject"):
            parts = {part.lower() for part in uproject.parts}
            if {"intermediate", "saved", "deriveddatacache"} & parts:
                continue
            candidates.append(uproject.parent.resolve())
    unique = sorted({str(path): path for path in candidates}.values(), key=lambda path: str(path).lower())
    if len(unique) == 1:
        return unique[0]
    if not unique:
        raise RuntimeError(
            "No real Unreal project was found. Pass --project with a Git-backed .uproject project. "
            "The final portfolio verifier intentionally does not substitute a generated fixture."
        )
    rendered = "\n".join(f"  - {path}" for path in unique[:30])
    raise RuntimeError(
        "Multiple Unreal projects were found. Pass --project explicitly so evidence cannot bind to the wrong project:\n"
        + rendered
    )


def find_editor(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit).expanduser().resolve()
        if not path.is_file():
            raise RuntimeError(f"UnrealEditor-Cmd does not exist: {path}")
        return path

    env_value = os.environ.get("UNREAL_EDITOR_CMD")
    if env_value:
        path = Path(env_value).expanduser().resolve()
        if path.is_file():
            return path

    candidates: list[Path] = []
    if os.name == "nt":
        program_files = Path(os.environ.get("ProgramFiles", r"C:\Program Files"))
        epic = program_files / "Epic Games"
        if epic.is_dir():
            candidates.extend(epic.glob("UE_*/Engine/Binaries/Win64/UnrealEditor-Cmd.exe"))
    else:
        candidates.extend(Path("/Users/Shared/Epic Games").glob("UE_*/Engine/Binaries/Mac/UnrealEditor-Cmd"))
        candidates.extend(Path("/opt").glob("UnrealEngine*/Engine/Binaries/Linux/UnrealEditor-Cmd"))

    candidates = [path.resolve() for path in candidates if path.is_file()]
    if not candidates:
        raise RuntimeError("UnrealEditor-Cmd was not found. Pass --unreal-editor-cmd or set UNREAL_EDITOR_CMD.")
    return sorted(candidates, key=lambda path: str(path), reverse=True)[0]


def target_revision(project: Path) -> tuple[str, str, str]:
    if not (project / ".git").exists():
        raise RuntimeError(
            "Real Unreal evidence requires a Git-backed target project so the analyzed revision is auditable."
        )
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
        description="Run the DIP Unreal collector through UnrealEditor-Cmd against a real Git-backed Unreal project."
    )
    parser.add_argument("--dip-root", default=str(Path(__file__).resolve().parents[1]))
    parser.add_argument("--project")
    parser.add_argument("--unreal-editor-cmd")
    parser.add_argument("--expected-dip-sha")
    parser.add_argument("--automation-filter", default="Project")
    parser.add_argument("--output", default="artifacts/real-project-runtime/unreal")
    parser.add_argument("--require-quality-pass", action="store_true")
    args = parser.parse_args()

    dip_root = Path(args.dip_root).expanduser().resolve()
    dip_sha, dip_branch = ensure_clean_dip(dip_root, args.expected_dip_sha)
    project = find_project(args.project)
    uprojects = list(project.glob("*.uproject"))
    if len(uprojects) != 1:
        raise RuntimeError(f"Expected exactly one .uproject at {project}; found {len(uprojects)}.")
    target_sha, target_branch, target_repository = target_revision(project)
    editor = find_editor(args.unreal_editor_cmd)

    output = (dip_root / args.output).resolve() if not Path(args.output).is_absolute() else Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    report_path = output / "unreal-real-project-report.json"
    evidence_path = output / "unreal-real-project-evidence.json"
    external_run_id = f"unreal-real-{dip_sha[:12]}-{uuid.uuid4().hex}"

    collector = dip_root / "collectors" / "unreal-collector" / "collector.py"
    command = [
        os.fspath(Path(os.sys.executable).resolve()),
        str(collector),
        "--root",
        str(project),
        "--project-id",
        PLACEHOLDER_PROJECT_ID,
        "--repository",
        target_repository,
        "--commit-sha",
        target_sha,
        "--branch",
        target_branch,
        "--external-run-id",
        external_run_id,
        "--attempt-number",
        "1",
        "--unreal-editor-cmd",
        str(editor),
        "--automation-filter",
        args.automation_filter,
        "--quality-gate",
        "critical",
        "--output",
        str(report_path),
    ]
    completed = run(command, cwd=dip_root, check=False)
    if completed.returncode not in {0, 2}:
        raise RuntimeError(f"Unreal collector failed with exit code {completed.returncode}.")
    if not report_path.is_file():
        raise RuntimeError("Unreal collector did not produce a report.")

    report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    required = {
        "externalRunId": external_run_id,
        "attemptNumber": 1,
        "repository": target_repository,
        "commitSha": target_sha,
        "branch": target_branch,
    }
    for key, expected in required.items():
        if report.get(key) != expected:
            raise RuntimeError(
                f"Unreal report identity mismatch for {key}: expected={expected!r} actual={report.get(key)!r}"
            )

    engine = report.get("engineNativeAutomation") or {}
    runtime_verified = bool(engine.get("attempted")) and not bool(engine.get("timedOut")) and bool(engine.get("report"))
    if not runtime_verified:
        raise RuntimeError(
            "UnrealEditor-Cmd was not proven to have completed engine-native automation with a report. "
            + json.dumps(engine, ensure_ascii=False)
        )

    quality_passed = completed.returncode == 0 and report.get("status") == "Succeeded"
    if args.require_quality_pass and not quality_passed:
        raise RuntimeError("Unreal engine runtime was verified, but the critical quality gate did not pass.")

    metrics = {str(item.get("name")): item.get("value") for item in report.get("metrics", [])}
    evidence = {
        "schema": "dip.real-project.unreal.v1",
        "status": "RuntimeVerified",
        "runtimeVerified": True,
        "qualityGate": "critical",
        "qualityGatePassed": quality_passed,
        "collectorStatus": report.get("status"),
        "dipSourceCommit": dip_sha,
        "dipSourceBranch": dip_branch,
        "targetProject": uprojects[0].stem,
        "targetRepository": target_repository,
        "targetCommit": target_sha,
        "targetBranch": target_branch,
        "unrealProject": str(uprojects[0]),
        "unrealEditorCmd": str(editor),
        "automationFilter": args.automation_filter,
        "engineNativeAutomation": engine,
        "collectorExitCode": completed.returncode,
        "externalRunId": external_run_id,
        "report": str(report_path),
        "metrics": metrics,
        "issueCount": len(report.get("issues", [])),
        "verifiedAtUtc": datetime.now(timezone.utc).isoformat(),
        "note": "dipSourceCommit identifies the DIP implementation under verification; targetCommit identifies the separate Unreal project revision analyzed by the engine-backed collector.",
    }
    evidence_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"RUNTIME VERIFIED: DIP {dip_sha} executed through UnrealEditor-Cmd against {uprojects[0].name} @ {target_sha}.")
    print(f"Quality gate passed: {quality_passed}")
    print(f"Evidence: {evidence_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
