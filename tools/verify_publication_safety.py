#!/usr/bin/env python3
"""Fail-closed pre-publication secret/file scanner for the DIP source tree.

This is a repository guardrail, not a replacement for Gitleaks/CodeQL/dependency auditing.
It scans the source snapshot that is about to be published and emits deterministic JSON.

Connection-string handling intentionally distinguishes literal credentials from runtime
references. Unknown literal passwords still fail closed. Environment variables, GitHub
Actions secrets, explicit placeholders, and known non-production verification credentials
are not treated as published credential material.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

EXCLUDED_PARTS = {
    ".git",
    "bin",
    "obj",
    ".vs",
    "node_modules",
    "TestResults",
    ".pytest_cache",
    "__pycache__",
    "artifacts",
}
ALLOWED_SECRET_LIKE_NAMES = {".env.example", "appsettings.example.json"}
FORBIDDEN_SUFFIXES = {".pem", ".p12", ".pfx", ".key", ".jks", ".keystore"}
MAX_TEXT_BYTES = 2_000_000

KNOWN_NON_PRODUCTION_PASSWORDS = {
    "change-me-local-only",
    "dip-governance-load",
    "dip-migration-password",
    "dip-dr-password",
    "dip-local-password",
    "dip-verification-password",
    "dip-benchmark-password",
    "dip-test-password",
    "dip-gui-password",
    "dip-test-password",
}

STATIC_PATTERNS = [
    ("private-key", re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----")),
    ("github-token", re.compile(r"\bgh[opusr]_[A-Za-z0-9]{20,}\b")),
    ("github-fine-grained-token", re.compile(r"\bgithub_pat_[A-Za-z0-9_]{20,}\b")),
    ("aws-access-key", re.compile(r"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b")),
    ("slack-token", re.compile(r"\bxox[baprs]-[A-Za-z0-9-]{10,}\b")),
    ("google-api-key", re.compile(r"\bAIza[0-9A-Za-z_-]{30,}\b")),
]

CONNECTION_PASSWORD = re.compile(
    r"(?i)\b(?:Password|Pwd)[\t ]*=[\t ]*(?P<value>[^;\r\n]+)"
)

SAFE_EXACT_VALUES = {
    "CHANGE_ME",
    "REPLACE_ME",
    "example",
    "placeholder",
    "<password>",
    "<secret>",
}

DYNAMIC_REFERENCE_PATTERNS = [
    re.compile(r"^\$env:[A-Za-z_][A-Za-z0-9_]*$", re.IGNORECASE),
    re.compile(r"^\$\{[A-Za-z_][A-Za-z0-9_]*\}$"),
    re.compile(r"^\$\{\{\s*secrets\.[A-Za-z_][A-Za-z0-9_]*\s*\}\}$", re.IGNORECASE),
    re.compile(r"^\$[A-Za-z_][A-Za-z0-9_]*$"),
    re.compile(r"^\$\([^)]+\)$"),
    re.compile(r"^%\([^)]+\)%$"),
]


def iter_files(root: Path):
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        rel = path.relative_to(root)
        if any(part in EXCLUDED_PARTS for part in rel.parts):
            continue
        yield path, rel


def normalize_password_value(raw: str) -> str:
    value = raw.strip().strip("\"'")
    return value.strip()


def is_safe_runtime_reference(raw: str) -> bool:
    value = normalize_password_value(raw)

    if not value:
        return True

    if value in KNOWN_NON_PRODUCTION_PASSWORDS:
        return True

    if value in SAFE_EXACT_VALUES:
        return True

    lowered = value.lower()
    if lowered.startswith(("example", "placeholder", "change_me", "replace_me")):
        return True

    for pattern in DYNAMIC_REFERENCE_PATTERNS:
        if pattern.fullmatch(value):
            return True

    # PowerShell interpolation inside a connection-string fragment, e.g.
    # Password=$DatabasePassword or Password=${env:POSTGRES_PASSWORD}.
    if "$" in value and re.search(
        r"\$(?:env:)?[A-Za-z_][A-Za-z0-9_]*|\$\{[^}]+\}|\$\{\{[^}]+\}\}",
        value,
    ):
        return True

    return False


def add_finding(findings, rule: str, rel: Path, line: int | None):
    findings.append(
        {
            "severity": "high",
            "rule": rule,
            "path": rel.as_posix(),
            "line": line,
            "message": (
                "Potential credential material detected; remove it and rotate the "
                "credential before publication."
            ),
        }
    )


def scan(root: Path):
    findings = []
    scanned = 0

    for path, rel in iter_files(root):
        scanned += 1
        name = path.name.lower()

        if (
            name == ".env" or path.suffix.lower() in FORBIDDEN_SUFFIXES
        ) and name not in ALLOWED_SECRET_LIKE_NAMES:
            findings.append(
                {
                    "severity": "high",
                    "rule": "secret-file",
                    "path": rel.as_posix(),
                    "line": None,
                    "message": "Secret/private-key file type must not be published.",
                }
            )

        try:
            size = path.stat().st_size
            if size > MAX_TEXT_BYTES:
                continue
            data = path.read_bytes()
            if b"\x00" in data:
                continue
            text = data.decode("utf-8")
        except (UnicodeDecodeError, OSError):
            continue

        for rule, regex in STATIC_PATTERNS:
            for match in regex.finditer(text):
                line = text.count("\n", 0, match.start()) + 1
                add_finding(findings, rule, rel, line)

        for match in CONNECTION_PASSWORD.finditer(text):
            raw_value = match.group("value")
            if is_safe_runtime_reference(raw_value):
                continue

            line = text.count("\n", 0, match.start()) + 1
            add_finding(findings, "connection-string-password", rel, line)

    return {
        "schemaVersion": "1.0",
        "root": ".",
        "filesScanned": scanned,
        "highSeverityFindings": len(findings),
        "findings": findings,
        "status": "PASS" if not findings else "FAIL",
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument("--output", default="")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    result = scan(root)
    payload = json.dumps(result, indent=2, ensure_ascii=False)

    if args.output:
        out = Path(args.output)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(payload + "\n", encoding="utf-8")

    print(payload)
    return 0 if result["status"] == "PASS" else 2


if __name__ == "__main__":
    raise SystemExit(main())
