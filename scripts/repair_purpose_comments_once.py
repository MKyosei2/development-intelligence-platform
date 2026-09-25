#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCAN_ROOTS = ("src", "tests", "scripts", "tools", "services", "collectors", "plugins")
EXTENSIONS = {".cs", ".razor", ".ps1", ".py"}

COMMON_SHORT = {
    "a", "an", "as", "at", "be", "by", "do", "for", "if", "in", "is",
    "it", "no", "of", "on", "or", "so", "the", "to", "up", "via", "we",
    "and", "not", "out", "per", "run", "use",
}
KNOWN_ACRONYMS = {
    "ai", "api", "ci", "cli", "css", "db", "dip", "e2e", "git", "gui",
    "html", "http", "https", "id", "ide", "json", "pm", "pr", "qa", "raci",
    "sha", "sql", "ui", "utc", "ux", "xml",
}
DISPLAY_MAP = {
    "Api": "API", "AI": "AI", "Ai": "AI", "Ci": "CI", "Cli": "CLI",
    "Cpp": "C++", "CSharp": "C#", "Css": "CSS", "Db": "DB", "Dip": "DIP",
    "E2E": "E2E", "E2e": "E2E", "Git": "Git", "GitHub": "GitHub",
    "Github": "GitHub", "Gui": "GUI", "Html": "HTML", "Http": "HTTP",
    "Https": "HTTPS", "Id": "ID", "Ide": "IDE", "Json": "JSON",
    "Pm": "PM", "Pr": "PR", "Qa": "QA", "Raci": "RACI", "Sha": "SHA",
    "Sql": "SQL", "Ui": "UI", "Utc": "UTC", "Ux": "UX",
    "Xlsx": "XLSX", "Xml": "XML",
}

PURPOSE_PATTERNS = (
    re.compile(r"^(?P<prefix>\s*//\s*Purpose:\s*)(?P<body>.*?)(?P<suffix>\s*)$"),
    re.compile(r"^(?P<prefix>\s*#\s*Purpose:\s*)(?P<body>.*?)(?P<suffix>\s*)$"),
    re.compile(r"^(?P<prefix>\s*@\*\s*Purpose:\s*)(?P<body>.*?)(?P<suffix>\s*\*@\s*)$"),
)


def purpose_match(line: str):
    for pattern in PURPOSE_PATTERNS:
        match = pattern.match(line)
        if match:
            return match
    return None


def is_fragmented(body: str) -> bool:
    tokens = re.findall(r"[A-Za-z][A-Za-z0-9+#.-]*", body)
    streak = 0
    for token in tokens:
        normalized = re.sub(r"[^A-Za-z]", "", token).lower()
        suspicious = (
            1 <= len(normalized) <= 3
            and normalized not in COMMON_SHORT
            and normalized not in KNOWN_ACRONYMS
        )
        if suspicious:
            streak += 1
            if streak >= 3:
                return True
        else:
            streak = 0
    return False


def split_identifier(value: str) -> list[str]:
    value = value.replace("-", " ").replace("_", " ")
    raw_parts: list[str] = []
    for part in value.split():
        raw_parts.extend(
            re.findall(
                r"[A-Z]+(?=[A-Z][a-z]|\d|$)|[A-Z]?[a-z]+|\d+",
                part,
            )
        )
    return raw_parts


def humanize_words(value: str) -> str:
    return " ".join(DISPLAY_MAP.get(part, part) for part in split_identifier(value))


def humanize_stem(path: Path) -> str:
    name = path.name
    if name.endswith(".razor.cs"):
        stem = name[:-9]
    else:
        stem = path.stem

    for suffix in ("Controller", "Tests", "Test"):
        if stem.endswith(suffix) and len(stem) > len(suffix):
            stem = stem[: -len(suffix)]

    return humanize_words(stem) or stem


def area(path: Path) -> str:
    parts = path.parts
    if "Features" in parts:
        index = parts.index("Features")
        if index + 1 < len(parts):
            return humanize_words(parts[index + 1]).lower()
    if "Controllers" in parts:
        return "API"
    if "Persistence" in parts:
        return "persistence"
    if "QualityAutomation" in parts:
        return "quality automation"
    if "ProjectIntelligence" in parts:
        return "project intelligence"
    if "Management" in parts:
        return "management"
    if "Workers" in parts:
        return "worker runtime"
    return "this repository area"


def replacement_body(path: Path) -> str:
    display = humanize_stem(path)
    posix = path.as_posix()

    if posix.startswith("scripts/"):
        return f"Runs {display} as part of repository development and verification workflows."
    if posix.startswith("tools/"):
        return f"Provides {display} tooling for repository verification and development workflows."
    if posix.startswith("tests/"):
        return f"Verifies {display} behavior, edge cases, and repository invariants."
    if posix.startswith("src/Dip.Api/Controllers/"):
        return f"Exposes {display} HTTP endpoints for the DIP API."
    if posix.startswith("src/Dip.Application/"):
        return f"Implements {display} application behavior for {area(path)}."
    if posix.startswith("src/Dip.Domain/"):
        return f"Defines {display} domain behavior and invariants for {area(path)}."
    if posix.startswith("src/Dip.Infrastructure/"):
        return f"Implements {display} infrastructure behavior for {area(path)}."
    if posix.startswith("src/Dip.Web/") and path.suffix == ".razor":
        return f"Renders the {display} UI and coordinates its interactions."
    if posix.startswith("src/Dip.Web/"):
        return f"Implements {display} web behavior for {area(path)}."
    if posix.startswith("src/Dip.Worker/"):
        return f"Runs {display} background processing for the DIP worker runtime."
    if posix.startswith("services/"):
        return f"Implements {display} service behavior for the DIP runtime."
    if posix.startswith("collectors/"):
        return f"Implements {display} collector behavior for external runtime evidence."
    if posix.startswith("plugins/"):
        return f"Implements {display} plugin behavior for DIP integrations."
    return f"Implements {display} behavior for this repository area."


def rewrite_file(path: Path, apply: bool) -> tuple[bool, str | None, str | None]:
    raw = path.read_bytes()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    lines = text.splitlines(keepends=True)
    changed = False
    old_line = None
    new_line = None

    for index, line in enumerate(lines):
        ending = "\r\n" if line.endswith("\r\n") else "\n" if line.endswith("\n") else ""
        content = line[:-len(ending)] if ending else line
        match = purpose_match(content)
        if not match:
            continue

        body = match.group("body").strip()
        if not is_fragmented(body):
            break

        new_body = replacement_body(path.relative_to(ROOT))
        replacement = f"{match.group('prefix')}{new_body}{match.group('suffix')}{ending}"
        old_line = content.strip()
        new_line = replacement.rstrip("\r\n").strip()
        lines[index] = replacement
        changed = True
        break

    if changed and apply:
        payload = "".join(lines)
        encoding = "utf-8-sig" if had_bom else "utf-8"
        path.write_text(payload, encoding=encoding, newline="")

    return changed, old_line, new_line


def iter_files():
    for root_name in SCAN_ROOTS:
        root = ROOT / root_name
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in EXTENSIONS:
                continue
            if any(part in {".git", "bin", "obj", "node_modules", "artifacts"} for part in path.parts):
                continue
            yield path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    findings = []
    for path in iter_files():
        changed, old_line, new_line = rewrite_file(path, apply=not args.check)
        if changed:
            findings.append((path.relative_to(ROOT).as_posix(), old_line, new_line))

    if args.check:
        if findings:
            print(f"Malformed Purpose comments remain: {len(findings)}")
            for path, old_line, _ in findings:
                print(f" - {path}: {old_line}")
            return 2
        print("No malformed Purpose comments remain.")
        return 0

    print(f"Repaired malformed Purpose comments: {len(findings)}")
    for path, old_line, new_line in findings:
        print(f" - {path}")
        print(f"   OLD: {old_line}")
        print(f"   NEW: {new_line}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
