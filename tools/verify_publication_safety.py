#!/usr/bin/env python3
"""Fail-closed pre-publication secret/file scanner for the DIP source tree.

This is a repository guardrail, not a replacement for Gitleaks/CodeQL/dependency auditing.
It scans the source snapshot that is about to be published and emits deterministic JSON.
"""
from __future__ import annotations
import argparse, json, os, re, sys
from pathlib import Path

EXCLUDED_PARTS={'.git','bin','obj','.vs','node_modules','TestResults','.pytest_cache','__pycache__','artifacts'}
ALLOWED_SECRET_LIKE_NAMES={'.env.example','appsettings.example.json'}
FORBIDDEN_SUFFIXES={'.pem','.p12','.pfx','.key','.jks','.keystore'}
MAX_TEXT_BYTES=2_000_000
KNOWN_NON_PRODUCTION_PASSWORDS={
    'change-me-local-only','dip-governance-load','dip-migration-password','dip-dr-password',
    'dip-local-password','dip-verification-password','dip-benchmark-password','dip-test-password'
}
PATTERNS=[
    ('private-key', re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----')),
    ('github-token', re.compile(r'\bgh[opusr]_[A-Za-z0-9]{20,}\b')),
    ('github-fine-grained-token', re.compile(r'\bgithub_pat_[A-Za-z0-9_]{20,}\b')),
    ('aws-access-key', re.compile(r'\b(?:AKIA|ASIA)[0-9A-Z]{16}\b')),
    ('slack-token', re.compile(r'\bxox[baprs]-[A-Za-z0-9-]{10,}\b')),
    ('google-api-key', re.compile(r'\bAIza[0-9A-Za-z_-]{30,}\b')),
    ('connection-string-password', re.compile(r'(?i)(?:Password|Pwd)[\t ]*=[\t ]*(?!\$\{|<|CHANGE_ME|REPLACE_ME|example|placeholder)[^;\s]{6,}')),
]

def iter_files(root:Path):
    for p in root.rglob('*'):
        if not p.is_file(): continue
        rel=p.relative_to(root)
        if any(part in EXCLUDED_PARTS for part in rel.parts): continue
        yield p, rel

def scan(root:Path):
    findings=[]; scanned=0
    for p,rel in iter_files(root):
        scanned+=1
        name=p.name.lower()
        if (name=='.env' or p.suffix.lower() in FORBIDDEN_SUFFIXES) and name not in ALLOWED_SECRET_LIKE_NAMES:
            findings.append({'severity':'high','rule':'secret-file','path':rel.as_posix(),'line':None,'message':'Secret/private-key file type must not be published.'})
        try:
            size=p.stat().st_size
            if size>MAX_TEXT_BYTES: continue
            data=p.read_bytes()
            if b'\x00' in data: continue
            text=data.decode('utf-8')
        except (UnicodeDecodeError,OSError):
            continue
        for rule,rx in PATTERNS:
            for m in rx.finditer(text):
                if rule == 'connection-string-password':
                    value=m.group(0).split('=',1)[-1].strip().strip('\"\'')
                    if value in KNOWN_NON_PRODUCTION_PASSWORDS:
                        continue
                line=text.count('\n',0,m.start())+1
                findings.append({'severity':'high','rule':rule,'path':rel.as_posix(),'line':line,'message':'Potential credential material detected; remove it and rotate the credential before publication.'})
    return {'schemaVersion':'1.0','root':'.','filesScanned':scanned,'highSeverityFindings':len(findings),'findings':findings,'status':'PASS' if not findings else 'FAIL'}

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',default='.'); ap.add_argument('--output',default='')
    a=ap.parse_args(); root=Path(a.root).resolve(); result=scan(root)
    payload=json.dumps(result,indent=2,ensure_ascii=False)
    if a.output:
        out=Path(a.output); out.parent.mkdir(parents=True,exist_ok=True); out.write_text(payload+'\n',encoding='utf-8')
    print(payload)
    return 0 if result['status']=='PASS' else 2
if __name__=='__main__': raise SystemExit(main())
