#!/usr/bin/env python3
# Purpose: Validates portfolio structure and evidence provenance without hard-coding historical measurement values.
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

REQUIRED = [
    "README.md",
    "docs/09-portfolio/architecture.svg",
    "docs/09-portfolio/ARCHITECTURE_OVERVIEW.md",
    "docs/09-portfolio/demo/dip-portfolio-demo.gif",
    "docs/09-portfolio/screenshots/project-intelligence.png",
    "docs/09-portfolio/screenshots/qa-defect.png",
    "docs/09-portfolio/screenshots/management-operations.png",
    "docs/09-portfolio/evidence/verified-baseline/README.md",
    "docs/09-portfolio/evidence/verified-baseline/benchmark-results.json",
    "docs/09-portfolio/evidence/verified-baseline/qa-bug-zoo-results.json",
    "docs/09-portfolio/evidence/verified-baseline/WINDOWS_DOTNET_RUNTIME_PASS.json",
    "src/Dip.Web/Pages/Dashboard/ManagementOperations.razor.cs",
    "src/Dip.Web/Components/Management/ManagementCockpitOverview.razor",
    "src/Dip.Web/Components/Management/ManagementSectionTabs.razor",
]
HEX40 = re.compile(r"^[0-9a-fA-F]{40}$")
HEX64 = re.compile(r"^[0-9a-fA-F]{64}$")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_object(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"JSON root must be an object: {path}")
    return value


def git_sha(root: Path) -> str | None:
    cp = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "HEAD"],
        text=True,
        capture_output=True,
        check=False,
    )
    value = cp.stdout.strip()
    return value if cp.returncode == 0 and HEX40.fullmatch(value) else None


def metadata_row(text: str, evidence_name: str) -> tuple[str, str] | None:
    for line in text.splitlines():
        if evidence_name not in line or not line.lstrip().startswith("|"):
            continue
        cells = [cell.strip().strip("`") for cell in line.split("|")[1:-1]]
        if len(cells) < 5:
            continue
        workflow_head = cells[2]
        declared_artifact_hash = cells[4]
        if HEX40.fullmatch(workflow_head) and HEX64.fullmatch(declared_artifact_hash):
            return workflow_head.lower(), declared_artifact_hash.lower()
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument("--output", default="")
    parser.add_argument("--require-current-final-evidence", action="store_true")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    checks: list[dict] = []

    def add(name: str, ok: bool, detail: str) -> None:
        checks.append({"name": name, "status": "PASS" if ok else "FAIL", "detail": detail})

    missing = [item for item in REQUIRED if not (root / item).is_file()]
    add("required-portfolio-files", not missing, "all required files present" if not missing else "missing: " + ", ".join(missing))

    readme = root / "README.md"
    size = readme.stat().st_size if readme.exists() else 0
    add("readme-size", 0 < size <= 12000, f"{size} bytes (target <= 12000)")

    web_root = root / "src/Dip.Web"
    empties = [p.relative_to(root).as_posix() for p in web_root.rglob("*.razor") if p.stat().st_size == 0] if web_root.is_dir() else []
    add("no-empty-razor", not empties, "none" if not empties else ", ".join(empties))

    monolith = root / "src/Dip.Web/Pages/Dashboard/ManagementOperations.razor"
    msize = monolith.stat().st_size if monolith.exists() else 0
    add("management-component-split", 0 < msize < 36000, f"ManagementOperations.razor={msize} bytes; code-behind + reusable components required")

    baseline_root = root / "docs/09-portfolio/evidence/verified-baseline"
    baseline_readme = baseline_root / "README.md"
    baseline_text = baseline_readme.read_text(encoding="utf-8-sig") if baseline_readme.is_file() else ""

    metadata = {
        "benchmark": metadata_row(baseline_text, "benchmark-results.*"),
        "qaBugZoo": metadata_row(baseline_text, "qa-bug-zoo-results.*"),
        "windowsDotNet": metadata_row(baseline_text, "WINDOWS_DOTNET_RUNTIME_PASS.json"),
    }
    add(
        "historical-artifact-hash-metadata",
        all(value is not None for value in metadata.values()),
        "verified-baseline README declares a 40-char workflow SHA and 64-char artifact SHA-256 for every historical evidence family",
    )

    try:
        path = baseline_root / "benchmark-results.json"
        benchmark = load_object(path)
        provenance = benchmark.get("provenance") or {}
        ok = (
            isinstance(benchmark.get("schemaVersion"), str)
            and isinstance(benchmark.get("generatedAtUtc"), str)
            and HEX40.fullmatch(str(provenance.get("commit", ""))) is not None
            and isinstance(benchmark.get("targets"), dict)
            and isinstance(benchmark.get("measurements"), dict)
            and bool(benchmark.get("measurements"))
        )
        add("historical-benchmark-schema-provenance", ok, f"embeddedCommit={provenance.get('commit','?')} localSha256={sha256(path)}")
    except Exception as exc:
        add("historical-benchmark-schema-provenance", False, str(exc))

    try:
        path = baseline_root / "qa-bug-zoo-results.json"
        qa = load_object(path)
        percentages = [qa.get("detectionRecallPercent"), qa.get("falsePositiveRatePercent"), qa.get("reproductionRatePercent")]
        ok = (
            isinstance(qa.get("schemaVersion"), str)
            and isinstance(qa.get("generatedAt"), str)
            and isinstance(qa.get("languages"), list)
            and len(qa.get("languages", [])) > 0
            and isinstance(qa.get("results"), list)
            and all(isinstance(value, (int, float)) and 0 <= value <= 100 for value in percentages)
        )
        workflow_head = metadata["qaBugZoo"][0] if metadata["qaBugZoo"] else "?"
        add("historical-qa-schema-provenance", ok, f"workflowHead={workflow_head} localSha256={sha256(path)}")
    except Exception as exc:
        add("historical-qa-schema-provenance", False, str(exc))

    try:
        path = baseline_root / "WINDOWS_DOTNET_RUNTIME_PASS.json"
        runtime = load_object(path)
        steps = runtime.get("steps")
        ok = (
            isinstance(runtime.get("schemaVersion"), str)
            and HEX40.fullmatch(str(runtime.get("commit", ""))) is not None
            and runtime.get("status") == "WINDOWS_DOTNET_PASS"
            and isinstance(steps, list)
            and bool(steps)
            and all(isinstance(step, dict) and step.get("name") and step.get("status") for step in steps)
        )
        add("historical-windows-runtime-schema-provenance", ok, f"embeddedCommit={runtime.get('commit','?')} localSha256={sha256(path)}")
    except Exception as exc:
        add("historical-windows-runtime-schema-provenance", False, str(exc))

    current = git_sha(root)
    if current:
        summary = root / "artifacts/final-evidence" / current / "FINAL_EVIDENCE_SUMMARY.json"
        if summary.is_file():
            try:
                value = load_object(summary)
                ok = value.get("status") == "PASS" and str(value.get("sourceCommit", "")).lower() == current.lower()
                add("current-exact-revision-final-evidence", ok, f"sourceCommit={value.get('sourceCommit','?')} path={summary.relative_to(root)}")
            except Exception as exc:
                add("current-exact-revision-final-evidence", False, str(exc))
        elif args.require_current_final_evidence:
            add("current-exact-revision-final-evidence", False, f"missing for current SHA {current}")
        else:
            checks.append({
                "name": "current-exact-revision-final-evidence",
                "status": "NOT_PRESENT",
                "detail": f"No final evidence for current SHA {current}; historical measurements are not promoted to current claims.",
            })
    else:
        checks.append({"name": "current-exact-revision-final-evidence", "status": "NOT_EVALUATED", "detail": "Git HEAD unavailable."})

    failed = any(check["status"] == "FAIL" for check in checks)
    result = {"schemaVersion": "2.0", "status": "FAIL" if failed else "PASS", "checks": checks}
    payload = json.dumps(result, indent=2, ensure_ascii=False)
    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(payload + "\n", encoding="utf-8")
    print(payload)
    return 2 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
