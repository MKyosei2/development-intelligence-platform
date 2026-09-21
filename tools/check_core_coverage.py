#!/usr/bin/env python3
# Purpose: Enforces independent line-coverage floors for Project Truth, Release, and game-development QA core code.
from __future__ import annotations

import argparse
from pathlib import Path
import xml.etree.ElementTree as ET

AREAS = {
    "ProjectTruth": ("/Features/ProjectTruth/",),
    "Release": ("/Features/Release/",),
    "QA": ("/Features/QualityAutomation/", "/Features/QualityGovernance/"),
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


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", default="TestResults")
    parser.add_argument("--minimum", type=float, default=50.0)
    args = parser.parse_args()

    reports = sorted(Path(args.results).rglob("coverage.cobertura.xml"))
    if not reports:
        raise SystemExit("Core coverage gate failed: no coverage.cobertura.xml files were found.")

    by_area = collect(reports)
    failures: list[str] = []
    for area, by_file in by_area.items():
        covered = sum(c for c, _ in by_file.values())
        valid = sum(v for _, v in by_file.values())
        if valid == 0:
            failures.append(f"{area}: no core lines were found")
            print(f"{area}: no covered source lines were discovered")
            continue
        pct = covered * 100.0 / valid
        print(f"{area} coverage: {covered}/{valid} lines = {pct:.2f}% (minimum {args.minimum:.2f}%)")
        for name, (c, v) in sorted(by_file.items(), key=lambda item: (item[1][0] / item[1][1], item[0]))[:10]:
            print(f"  {c * 100.0 / v:6.2f}%  {name}")
        if pct + 1e-9 < args.minimum:
            failures.append(f"{area}: {pct:.2f}% < {args.minimum:.2f}%")

    if failures:
        raise SystemExit("Core coverage gate failed: " + "; ".join(failures))
    print("PASS: Project Truth, Release, and QA each satisfy the focused coverage floor.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
