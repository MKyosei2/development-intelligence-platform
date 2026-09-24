#!/usr/bin/env python3
# Purpose: Verifies the staged Actions policy and rejects workflows that directly mutate the main branch.
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORKFLOWS = ROOT / ".github" / "workflows"
FINAL = {
    "commit-message.yml",
    "dotnet-ci.yml",
    "python-ci.yml",
    "native-collector-ci.yml",
    "integration-test.yml",
    "gui-e2e.yml",
    "docker-build.yml",
    "security-scan.yml",
    "universal-qa-benchmark.yml",
    "unreal-collector-ci.yml",
    "publication-readiness.yml",
}
FORBIDDEN = {
    "pull_request",
    "pull_request_target",
    "schedule",
    "workflow_run",
    "repository_dispatch",
}
OBSOLETE_MUTATION_WORKFLOWS = {
    "normalize-purpose-once.yml",
}


def on_block(text: str) -> str:
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    start = next((i for i, line in enumerate(lines) if re.fullmatch(r"on\s*:\s*", line)), -1)
    if start < 0:
        raise ValueError("block-form top-level on: was not found")
    end = len(lines)
    for i in range(start + 1, len(lines)):
        if re.match(r"^[A-Za-z_][A-Za-z0-9_.-]*\s*:", lines[i]):
            end = i
            break
    return "\n".join(lines[start:end])


def events(block: str) -> set[str]:
    return {
        match.group(1)
        for line in block.splitlines()
        if (match := re.match(r"^  ([A-Za-z_][A-Za-z0-9_-]*)\s*:", line))
    }


def direct_main_pushes(text: str) -> list[str]:
    pushes: list[str] = []
    for line in text.replace("\r\n", "\n").splitlines():
        stripped = line.strip()
        if not re.search(r"\bgit\s+push\b", stripped, re.IGNORECASE):
            continue
        if re.search(r"HEAD:main\b|\brefs/heads/main\b|\borigin\s+main\b|\bmain:main\b", stripped):
            pushes.append(stripped)
    return pushes


def main() -> int:
    failures: list[str] = []
    files = sorted([*WORKFLOWS.glob("*.yml"), *WORKFLOWS.glob("*.yaml")])
    names = {path.name for path in files}

    missing = FINAL - names
    failures.extend(f"required final workflow missing: {name}" for name in sorted(missing))

    for obsolete in sorted(OBSOLETE_MUTATION_WORKFLOWS & names):
        failures.append(f"obsolete source-mutating workflow must be deleted: {obsolete}")

    for path in files:
        text = path.read_text(encoding="utf-8-sig")
        try:
            block = on_block(text)
        except Exception as exc:
            failures.append(f"{path.name}: {exc}")
            continue

        ev = events(block)
        if "workflow_dispatch" not in ev:
            failures.append(f"{path.name}: workflow_dispatch missing")
        for event in sorted(FORBIDDEN & ev):
            failures.append(f"{path.name}: forbidden automatic trigger {event}")

        if path.name in FINAL:
            if "push" not in ev:
                failures.append(f"{path.name}: final push trigger missing")
            if "branches: [main]" not in block:
                failures.append(f"{path.name}: final push is not restricted to main")
            if ".portfolio-ci-trigger" not in block:
                failures.append(f"{path.name}: final trigger path missing")
        elif "push" in ev:
            failures.append(f"{path.name}: non-final workflow still has push trigger")

        main_pushes = direct_main_pushes(text)
        if main_pushes:
            failures.append(
                f"{path.name}: workflow directly pushes to main; publish a patch/PR instead: "
                + " | ".join(main_pushes)
            )

        writes_contents = re.search(r"(?m)^\s*contents\s*:\s*write\s*$", text) is not None
        source_repair = (
            "Repair-SourcePurposeComments.ps1" in text
            or "Repair-SourceReadabilityResiduals.ps1" in text
        )
        if writes_contents and source_repair:
            failures.append(
                f"{path.name}: source-repair workflow must not request contents: write; use a reviewable artifact or PR path"
            )

    if failures:
        print("ACTIONS POLICY FAILED", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
        return 1

    print("ACTIONS POLICY PASS")
    print("Stage 1: work/feature/fix/chore branch push and PR -> automatic hosted Actions 0")
    print("Stage 2: workflow_dispatch -> Fast / Targeted / Full manual execution")
    print("Stage 3: main + .portfolio-ci-trigger -> 11 required workflows")
    print("Source repair: never pushes directly to main; repair output must be reviewable before merge")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
