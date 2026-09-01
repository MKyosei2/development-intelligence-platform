#!/usr/bin/env python3
"""Fail-closed pre-publication secret/file scanner for DIP.

Literal production-like credentials remain hard failures. Runtime references
(environment variables, PowerShell variables and GitHub Actions secrets),
explicit placeholders and known non-production verification credentials are
not treated as published secrets.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

EXCLUDED_PARTS = {
    ".git", "bin", "obj", ".vs", "node_modules", "TestResults",
    ".pytest_cache", "__pycache__", "artifacts", ".dip",
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
}

STATIC_PATTERNS = [
    ("private-key", re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----")),
    ("github-token", re.compile(r"\bgh[opusr]_[A-Za-z0-9]{20,}\b")),
    ("github-fine-grained-token", re.compile(r"\bgithub_pat_[A-Za-z0-9_]{20,}\b")),
    ("aws-access-key", re.compile(r"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b")),
    ("slack-token", re.compile(r"\bxox[baprs]-[A-Za-z0-9-]{10,}\b")),
    ("google-api-key", re.compile(r"\bAIza[0-9A-Za-z_-]{30,}\b")),
]

# Stop at the connection-string semicolon OR the source-language quote delimiter.
# The prior pattern consumed a closing JSON/C#/PowerShell quote (for example
# Password=dip-local-password"), which prevented known non-production values
# from matching the allow-list and produced false high-severity findings.
CONNECTION_PASSWORD = re.compile(
    r"""(?i)\b(?:Password|Pwd)[\t ]*=[\t ]*(?P<value>[^;'"`\r\n]+)"""
)

SAFE_PLACEHOLDERS = {
    "CHANGE_ME", "REPLACE_ME", "example", "placeholder",
    "<password>", "<secret>",
}

DYNAMIC_PATTERNS = [
    re.compile(r"^\$env:[A-Za-z_][A-Za-z0-9_]*$", re.IGNORECASE),
    re.compile(r"^\$\{env:[A-Za-z_][A-Za-z0-9_]*\}$", re.IGNORECASE),
    re.compile(r"^\$\{[A-Za-z_][A-Za-z0-9_]*\}$"),
    re.compile(r"^\$\{\{\s*secrets\.[A-Za-z_][A-Za-z0-9_]*\s*\}\}$", re.IGNORECASE),
    re.compile(r"^\$[A-Za-z_][A-Za-z0-9_]*$"),
    re.compile(r"^\$\([^)]+\)$"),
]


def iter_files(root: Path):
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        rel = path.relative_to(root)
        if any(part in EXCLUDED_PARTS for part in rel.parts):
            continue
        yield path, rel


def normalize(raw: str) -> str:
    value = raw.strip()
    while len(value) >= 2 and (
        (value[0] == '"' and value[-1] == '"')
        or (value[0] == "'" and value[-1] == "'")
    ):
        value = value[1:-1].strip()
    return value


def is_safe_runtime_reference(raw: str) -> bool:
    value = normalize(raw)
    if not value:
        return True
    if value in KNOWN_NON_PRODUCTION_PASSWORDS or value in SAFE_PLACEHOLDERS:
        return True
    if value.lower().startswith(("example", "placeholder", "change_me", "replace_me")):
        return True
    if any(pattern.fullmatch(value) for pattern in DYNAMIC_PATTERNS):
        return True

    if re.search(
        r"\$(?:env:)?[A-Za-z_][A-Za-z0-9_]*"
        r"|\$\{[^}]+\}"
        r"|\$\{\{[^}]+\}\}",
        value,
    ):
        return True
    return False


def finding(rule: str, rel: Path, line: int | None, message: str):
    return {
        "severity": "high",
        "rule": rule,
        "path": rel.as_posix(),
        "line": line,
        "message": message,
    }


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
                finding(
                    "secret-file",
                    rel,
                    None,
                    "Secret/private-key file type must not be published.",
                )
            )

        try:
            if path.stat().st_size > MAX_TEXT_BYTES:
                continue
            data = path.read_bytes()
            if b"\x00" in data:
                continue
            text = data.decode("utf-8")
        except (UnicodeDecodeError, OSError):
            continue

        for rule, regex in STATIC_PATTERNS:
            for match in regex.finditer(text):
                findings.append(
                    finding(
                        rule,
                        rel,
                        text.count("\n", 0, match.start()) + 1,
                        "Potential credential material detected; remove it and rotate the credential before publication.",
                    )
                )

        for match in CONNECTION_PASSWORD.finditer(text):
            if is_safe_runtime_reference(match.group("value")):
                continue
            findings.append(
                finding(
                    "connection-string-password",
                    rel,
                    text.count("\n", 0, match.start()) + 1,
                    "Literal connection-string password detected. Use a runtime secret/environment reference or an explicitly non-production verification credential.",
                )
            )

    return {
        "schemaVersion": "1.1",
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

    result = scan(Path(args.root).resolve())
    payload = json.dumps(result, indent=2, ensure_ascii=False)

    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(payload + "\n", encoding="utf-8")

    print(payload)
    return 0 if result["status"] == "PASS" else 2


if __name__ == "__main__":
    raise SystemExit(main())
