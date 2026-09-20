#!/usr/bin/env python3
# Purpose: Enforces a focused line-coverage floor for Project Truth, Release, and game-development QA core code.
from __future__ import annotations
import argparse
from pathlib import Path
import xml.etree.ElementTree as ET

CORE_MARKERS = (
    "/Features/ProjectTruth/",
    "/Features/Release/",
    "/Features/QualityAutomation/",
    "/Features/QualityGovernance/",
)

def normalize(value: str) -> str:
    return "/" + value.replace("\\", "/").lstrip("./")

def collect(files: list[Path]) -> tuple[int, int, dict[str, tuple[int, int]]]:
    covered = valid = 0
    by_file: dict[str, tuple[int, int]] = {}
    for path in files:
        root = ET.parse(path).getroot()
        for cls in root.findall(".//class"):
            filename = normalize(cls.attrib.get("filename", ""))
            if not any(marker in filename for marker in CORE_MARKERS):
                continue
            file_valid = file_covered = 0
            for line in cls.findall("./lines/line"):
                hits = int(line.attrib.get("hits", "0"))
                file_valid += 1
                file_covered += int(hits > 0)
            if file_valid:
                old_cov, old_valid = by_file.get(filename, (0, 0))
                # Test projects can emit duplicate class entries. Keep the strongest observation per file.
                if file_valid > old_valid or (file_valid == old_valid and file_covered > old_cov):
                    by_file[filename] = (file_covered, file_valid)
    for file_covered, file_valid in by_file.values():
        covered += file_covered
        valid += file_valid
    return covered, valid, by_file

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", default="TestResults")
    parser.add_argument("--minimum", type=float, default=50.0)
    args = parser.parse_args()
    files = sorted(Path(args.results).rglob("coverage.cobertura.xml"))
    if not files:
        raise SystemExit("Core coverage gate failed: no coverage.cobertura.xml files were found.")
    covered, valid, by_file = collect(files)
    if valid == 0:
        raise SystemExit("Core coverage gate failed: no ProjectTruth/Release/QA core lines were found in coverage reports.")
    pct = covered * 100.0 / valid
    print(f"Core coverage: {covered}/{valid} lines = {pct:.2f}% (minimum {args.minimum:.2f}%)")
    for name, (c, v) in sorted(by_file.items(), key=lambda item: (item[1][0] / item[1][1], item[0]))[:15]:
        print(f"  {c * 100.0 / v:6.2f}%  {name}")
    if pct + 1e-9 < args.minimum:
        raise SystemExit(f"Core coverage gate failed: {pct:.2f}% < {args.minimum:.2f}%")
    print("PASS: focused core coverage gate satisfied.")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
