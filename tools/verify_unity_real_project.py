#!/usr/bin/env python3
# Purpose: Optionally verifies the DIP Unity collector against an explicitly supplied real Git-backed Unity project; not required for portfolio publication.
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import tempfile
import uuid
from datetime import datetime, timezone
from pathlib import Path

PLACEHOLDER_PROJECT_ID = "11111111-1111-1111-1111-111111111111"


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
        raise RuntimeError("Optional Unity runtime verification requires a clean tracked DIP working tree.\n" + dirty)
    branch = git_text(root, "branch", "--show-current") or "detached"
    return sha, branch


def require_project(explicit: str) -> Path:
    path = Path(explicit).expanduser().resolve()
    if not (path / "Assets").is_dir() or not (path / "ProjectSettings").is_dir():
        raise RuntimeError(f"Not a Unity project: {path}")
    if not (path / ".git").exists():
        raise RuntimeError("Optional Unity runtime evidence requires a Git-backed target project.")
    return path


def read_project_version(project: Path) -> str:
    version_file = project / "ProjectSettings" / "ProjectVersion.txt"
    if not version_file.is_file():
        raise RuntimeError(f"Unity ProjectVersion.txt is missing: {version_file}")
    for line in version_file.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        if line.startswith("m_EditorVersion:"):
            version = line.split(":", 1)[1].strip()
            if version:
                return version
    raise RuntimeError("Could not resolve m_EditorVersion from ProjectVersion.txt.")


def require_unity(explicit: str) -> Path:
    path = Path(explicit).expanduser().resolve()
    if not path.is_file():
        raise RuntimeError(f"Unity executable does not exist: {path}")
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


def copy_project(source: Path, destination: Path) -> None:
    ignored = {"Library", "Temp", "Logs", "obj", "Build", "Builds", ".git", ".vs"}

    def ignore(_: str, names: list[str]) -> set[str]:
        return {name for name in names if name in ignored}

    shutil.copytree(source, destination, ignore=ignore)


def inject_dip_package(project: Path, dip_root: Path) -> None:
    manifest_path = project / "Packages" / "manifest.json"
    if not manifest_path.is_file():
        raise RuntimeError(f"Unity package manifest is missing: {manifest_path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    dependencies = manifest.setdefault("dependencies", {})
    package = (
        dip_root / "collectors" / "unity-collector" / "Packages" / "com.mkyosei.dip.collector"
    ).resolve()
    dependencies["com.mkyosei.dip.collector"] = "file:" + package.as_posix()
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "OPTIONAL: run the DIP Unity collector inside an explicitly supplied real Git-backed Unity project. "
            "This tool is not part of the final portfolio publication gate."
        )
    )
    parser.add_argument("--dip-root", default=str(Path(__file__).resolve().parents[1]))
    parser.add_argument("--project", required=True)
    parser.add_argument("--unity", required=True, help="Path to the Unity Editor executable.")
    parser.add_argument("--expected-dip-sha")
    parser.add_argument("--output", default="artifacts/optional-engine-runtime/unity")
    parser.add_argument("--require-quality-pass", action="store_true")
    args = parser.parse_args()

    dip_root = Path(args.dip_root).expanduser().resolve()
    dip_sha, dip_branch = ensure_clean_dip(dip_root, args.expected_dip_sha)
    project = require_project(args.project)
    unity = require_unity(args.unity)
    target_sha, target_branch, target_repository = target_identity(project)
    project_version = read_project_version(project)

    output = (dip_root / args.output).resolve() if not Path(args.output).is_absolute() else Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="dip-unity-optional-") as temp_value:
        workspace = Path(temp_value)
        runtime_project = workspace / "runtime-project"
        copy_project(project, runtime_project)
        inject_dip_package(runtime_project, dip_root)

        report_path = output / "unity-optional-runtime-report.json"
        log_path = output / "unity-optional-runtime.log"
        external_run_id = f"unity-optional-{dip_sha[:12]}-{uuid.uuid4().hex}"
        command = [
            str(unity), "-batchmode", "-nographics", "-quit",
            "-projectPath", str(runtime_project),
            "-executeMethod", "Dip.UnityCollector.Editor.Cli.UnityCollectorExactRevisionCli.Run",
            "-dipProjectId", PLACEHOLDER_PROJECT_ID,
            "-dipRepository", target_repository,
            "-dipCommitSha", target_sha,
            "-dipBranch", target_branch,
            "-dipExternalRunId", external_run_id,
            "-dipAttemptNumber", "1",
            "-dipQualityGate", "critical",
            "-dipOutput", str(report_path),
            "-logFile", str(log_path),
        ]
        completed = run(command, check=False)
        if completed.returncode not in {0, 2}:
            raise RuntimeError(f"Unity process failed with exit code {completed.returncode}. See {log_path}")
        if not report_path.is_file():
            raise RuntimeError("Unity completed without producing the collector report.")

        report = json.loads(report_path.read_text(encoding="utf-8-sig"))
        for key, expected in {
            "externalRunId": external_run_id,
            "attemptNumber": 1,
            "repository": target_repository,
            "commitSha": target_sha,
            "branch": target_branch,
        }.items():
            if report.get(key) != expected:
                raise RuntimeError(f"Unity report identity mismatch for {key}: expected={expected!r} actual={report.get(key)!r}")

        runtime_verified = report.get("status") in {"Succeeded", "PartialSuccess", "Failed"}
        if not runtime_verified:
            raise RuntimeError(f"Unexpected Unity report status: {report.get('status')!r}")
        quality_passed = completed.returncode == 0 and report.get("status") in {"Succeeded", "PartialSuccess"}
        if args.require_quality_pass and not quality_passed:
            raise RuntimeError("Unity runtime was verified, but the critical quality gate did not pass.")

        evidence = {
            "schema": "dip.optional-engine.unity.v1",
            "status": "RuntimeVerified",
            "publicationRequirement": False,
            "runtimeVerified": True,
            "qualityGatePassed": quality_passed,
            "dipSourceCommit": dip_sha,
            "dipSourceBranch": dip_branch,
            "targetProject": project.name,
            "targetRepository": target_repository,
            "targetCommit": target_sha,
            "targetBranch": target_branch,
            "unityVersion": project_version,
            "unityExecutable": str(unity),
            "collectorExitCode": completed.returncode,
            "externalRunId": external_run_id,
            "report": str(report_path),
            "log": str(log_path),
            "verifiedAtUtc": datetime.now(timezone.utc).isoformat(),
            "note": "Optional runtime evidence only. Absence of this artifact does not block final portfolio publication.",
        }
        evidence_path = output / "unity-optional-runtime-evidence.json"
        evidence_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"OPTIONAL UNITY RUNTIME VERIFIED: DIP {dip_sha} against {target_repository}@{target_sha}")
        print(f"Evidence: {evidence_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
