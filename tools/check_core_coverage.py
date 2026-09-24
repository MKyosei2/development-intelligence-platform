#!/usr/bin/env python3
# Purpose: Enforces meaningful aggregate and critical-file line-coverage floors for portfolio-critical core code.
from __future__ import annotations

import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET

DEFAULT_MINIMUM = 50.0
DEFAULT_CRITICAL_MINIMUM = 65.0

AREAS = {
    "ProjectTruth": ("/Features/ProjectTruth/",),
    "Release": ("/Features/Release/",),
    "QA": ("/Features/QualityAutomation/", "/Features/QualityGovernance/"),
}

CRITICAL_FILES = {
    "ClaimVerificationEvaluator.cs": "ProjectTruth",
    "ProgressEvaluator.cs": "ProjectTruth",
    "ProjectTruthEvaluator.cs": "ProjectTruth",
    "EvidenceConfidenceEvaluator.cs": "ProjectTruth",
    "ReleaseEvidenceService.cs": "Release",
    "ReleaseReadinessService.cs": "Release",
    "ReleaseReadinessProjectTruthEvaluator.cs": "Release",
    "ReleaseReadinessQaEvaluator.cs": "Release",
    "QaExecutionPlanner.cs": "QA",
    "QaAdapterExecutionCoordinator.cs": "QA",
    "QaResultMaterializer.cs": "QA",
    "QaFindingProcessor.cs": "QA",
    "QaPostRunProcessor.cs": "QA",
    "QaFindingLifecycleProcessor.cs": "QA",
    "QaReliabilityProcessor.cs": "QA",
    "QaBaselineProcessor.cs": "QA",
    "QaPolicyEvaluator.cs": "QA",
    "QaProvenanceBuilder.cs": "QA",
    "QaHealthEvaluator.cs": "QA",
    "QaCostEvaluator.cs": "QA",
    "QaBenchmarkComparisonService.cs": "QA",
}


def normalize(value: str) -> str:
    return "/" + value.replace("\\", "/").lstrip("./")


def area_for(filename: str) -> str | None:
    for name, markers in AREAS.items():
        if any(marker in filename for marker in markers):
            return name
    return None


def collect(files: list[Path]) -> dict[str, dict[str, tuple[int, int]]]:
    by_area: dict[str, dict[str, tuple[int, int]]] = {name: {} for name in AREAS}
    for path in files:
        root = ET.parse(path).getroot()
        for cls in root.findall(".//class"):
            filename = normalize(cls.attrib.get("filename", ""))
            area = area_for(filename)
            if area is None:
                continue
            valid = covered = 0
            for line in cls.findall("./lines/line"):
                valid += 1
                covered += int(int(line.attrib.get("hits", "0")) > 0)
            if valid == 0:
                continue
            old_covered, old_valid = by_area[area].get(filename, (0, 0))
            if valid > old_valid or (valid == old_valid and covered > old_covered):
                by_area[area][filename] = (covered, valid)
    return by_area


def percentage(covered: int, valid: int) -> float:
    return covered * 100.0 / valid if valid else 0.0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", default="TestResults")
    parser.add_argument("--minimum", type=float, default=DEFAULT_MINIMUM)
    parser.add_argument("--critical-minimum", type=float, default=DEFAULT_CRITICAL_MINIMUM)
    parser.add_argument("--summary-json", default="TestResults/core-coverage-summary.json")
    args = parser.parse_args()

    for name, value in (("--minimum", args.minimum), ("--critical-minimum", args.critical_minimum)):
        if not 0.0 <= value <= 100.0:
            raise SystemExit(f"Core coverage gate failed: {name} must be between 0 and 100.")
    if args.critical_minimum <= 0:
        raise SystemExit("Core coverage gate failed: critical minimum must be greater than zero.")

    reports = sorted(Path(args.results).rglob("coverage.cobertura.xml"))
    if not reports:
        raise SystemExit("Core coverage gate failed: no coverage.cobertura.xml files were found.")

    by_area = collect(reports)
    failures: list[str] = []
    summary: dict[str, object] = {
        "aggregateMinimumPercent": args.minimum,
        "criticalMinimumPercent": args.critical_minimum,
        "areas": {},
        "criticalFiles": {},
    }

    for area, by_file in by_area.items():
        covered = sum(c for c, _ in by_file.values())
        valid = sum(v for _, v in by_file.values())
        if valid == 0:
            failures.append(f"{area}: no core lines were found")
            continue
        pct = percentage(covered, valid)
        summary["areas"][area] = {"covered": covered, "valid": valid, "percent": round(pct, 2)}
        print(f"{area} coverage: {covered}/{valid} lines = {pct:.2f}% (minimum {args.minimum:.2f}%)")
        if pct + 1e-9 < args.minimum:
            failures.append(f"{area}: {pct:.2f}% < {args.minimum:.2f}%")

    by_basename: dict[str, tuple[str, str, tuple[int, int]]] = {}
    for area, files_in_area in by_area.items():
        for name, counts in files_in_area.items():
            by_basename[Path(name).name] = (area, name, counts)

    for critical, expected_area in sorted(CRITICAL_FILES.items()):
        entry = by_basename.get(critical)
        if entry is None:
            failures.append(f"{critical}: no coverage entry was found")
            summary["criticalFiles"][critical] = {"present": False, "area": expected_area}
            continue
        actual_area, full_name, (covered, valid) = entry
        pct = percentage(covered, valid)
        summary["criticalFiles"][critical] = {
            "present": True,
            "area": actual_area,
            "path": full_name,
            "covered": covered,
            "valid": valid,
            "percent": round(pct, 2),
            "minimumPercent": args.critical_minimum,
        }
        if actual_area != expected_area:
            failures.append(f"{critical}: expected area {expected_area}, coverage entry resolved to {actual_area}")
        print(f"critical {expected_area}/{critical}: {covered}/{valid} = {pct:.2f}% (minimum {args.critical_minimum:.2f}%)")
        if pct + 1e-9 < args.critical_minimum:
            failures.append(f"{critical}: {pct:.2f}% < {args.critical_minimum:.2f}%")

    summary_path = Path(args.summary_json)
    summary_path.parent.mkdir(parents=True, exist_ok=True)
    summary["failures"] = failures
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    if failures:
        raise SystemExit("Core coverage gate failed: " + "; ".join(failures))

    print("PASS: aggregate portfolio areas and every split QA critical file satisfy meaningful coverage floors.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
