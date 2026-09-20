#!/usr/bin/env python3
# Purpose: Applies the requested source-quality refactor deterministically to an existing DIP checkout while preserving public contracts.
from __future__ import annotations
import argparse
import re
import shutil
import subprocess
from pathlib import Path

ROOTS = ("src", "tests", "scripts", "services", "collectors", "plugins", "tools")
SOURCE_EXTENSIONS = {".cs", ".razor", ".ps1", ".py", ".cpp", ".c", ".h", ".hpp", ".js", ".ts", ".tsx", ".jsx"}
EXCLUDED_PARTS = {".git", "bin", "obj", "node_modules", "packages", "vendor", "third_party", "ThirdParty", "Intermediate", "Binaries", "Library", "Temp", "Logs", "Migrations", "Generated", "generated"}

CSHARP_SPLITS: dict[str, dict[str, tuple[str, ...]]] = {
    "src/Dip.Application/Features/Management/ManagementOperationsService.cs": {
        "Governance": ("Approval", "Raci", "Responsibility", "RoleBinding", "ManagementRole"),
        "CalendarResources": ("Calendar", "SharedResource", "Allocation", "Resource"),
        "Alerts": ("Alert", "Notification", "Escalation"),
        "Sync": ("Sync", "Authority", "Conflict", "Sheet", "Google"),
    },
    "src/Dip.Application/Features/Management/ManagementAnalyticsService.cs": {
        "Capacity": ("Capacity", "Workload"),
        "Schedule": ("Schedule", "Critical", "Gantt", "Milestone", "Dependency"),
        "ScopeRisk": ("Scope", "Risk", "Decision", "Blocker", "Resource"),
        "Alerts": ("Alert", "Escalation"),
        "Workbook": ("Workbook", "Sheet", "Export", "Import", "Xlsx", "Google"),
    },
    "src/Dip.Application/Features/Management/ManagementSpreadsheetService.cs": {
        "Analysis": ("Analyze", "Schema", "Infer", "Detect"),
        "Import": ("Import", "Apply", "Parse", "Row", "Workbook"),
        "Sync": ("Sync", "Google", "Conflict", "Snapshot", "Hash"),
    },
    "src/Dip.Application/Features/Release/ReleaseReadinessService.cs": {
        "Operational": ("Incident", "DurableJob"),
        "Evidence": ("Performance", "FreshEvidence", "ReleaseEvidence"),
        "Qa": ("Qa", "Policy", "Override", "Provenance"),
        "ProjectTruth": ("ProjectTruth", "ChangeRisk"),
        "DevelopmentGraph": ("DevelopmentGraph", "Management", "Criterion", "Status"),
    },
    "src/Dip.Infrastructure/GitHub/GitHubRepositoryCatalog.cs": {
        "Repositories": ("Repository", "Owner", "Catalog"),
        "Branches": ("Branch", "Commit", "Ref"),
        "Workflows": ("Workflow", "Run", "Check", "Action"),
        "Releases": ("Release", "Tag", "Asset"),
    },
    "src/Dip.Application/Features/QualityGovernance/QaGovernanceCoreEngine.cs": {
        "Findings": ("Finding", "Flaky", "Ownership"),
        "Policy": ("Policy", "Gate", "Provenance"),
        "Waivers": ("Waiver", "Override"),
        "Health": ("Health", "Trend", "Reliability", "Recommend", "SelectTest"),
    },
    "src/Dip.Application/Features/QualityAutomation/QaOrchestrationService.cs": {
        "Planning": ("Plan", "Capability", "Adapter", "Detect"),
        "Execution": ("Run", "Execute", "Process", "Qa"),
        "Persistence": ("Persist", "Save", "Evidence", "Artifact", "Report"),
    },
    "src/Dip.Web/Services/ApiClient.cs": {
        "ProjectTruth": ("Claim", "Truth", "EvidenceAssessment", "ProjectProgress"),
        "Qa": ("Qa", "Governance", "Waiver", "Policy", "Finding", "Override"),
        "Release": ("Release", "Readiness", "Benchmark"),
        "Management": ("Management", "Portfolio", "Calendar", "Spreadsheet", "Sync", "Raci", "Approval"),
    },
}

TARGET_CSHARP_FORMAT = set(CSHARP_SPLITS) | {
    "src/Dip.Infrastructure/QualityAutomation/ProcessExecutor.cs",
    "src/Dip.Api/Controllers/GitHubRepositoriesController.cs",
}

RAZOR_CODEBEHIND = {
    "src/Dip.Web/Pages/Dashboard/Qa.razor",
    "src/Dip.Web/Pages/Settings.razor",
    "src/Dip.Web/Components/Layout/MainLayout.razor",
}
RAZOR_FORMAT = RAZOR_CODEBEHIND | {"src/Dip.Web/Pages/Dashboard/ManagementOperations.razor"}


def humanize(name: str) -> str:
    value = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", name)
    value = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1 \2", value)
    return re.sub(r"[_\-]+", " ", value).strip()


def purpose_for(path: Path, rel: str) -> str:
    stem = path.stem
    human = humanize(stem)
    if stem == "Program": return "Composes and starts this DIP host."
    if rel.startswith("tests/"): return f"Verifies the behavior and invariants exercised by {human}."
    if rel.startswith("scripts/") or rel.startswith("tools/"): return f"Automates and verifies the repository workflow represented by {human}."
    if path.suffix.lower() == ".razor": return f"Renders and coordinates the {human} web surface."
    if stem.endswith("Controller"): return f"Exposes the HTTP boundary implemented by {human}."
    if stem.endswith("Repository"): return f"Persists and queries data through {human}."
    if stem.endswith("Service"): return f"Orchestrates the workflow implemented by {human}."
    if rel.startswith("src/Dip.Domain/"): return f"Defines {human} in the DIP domain."
    if rel.startswith("src/Dip.Infrastructure/"): return f"Provides the infrastructure responsibility represented by {human}."
    if rel.startswith("src/Dip.Web/"): return f"Implements the web responsibility represented by {human}."
    return f"Defines the DIP responsibility represented by {human}."


def fragmented(purpose: str, stem: str) -> bool:
    target = re.sub(r"[^A-Za-z0-9]", "", stem).lower()
    if len(target) < 4: return False
    tokens = re.findall(r"[A-Za-z0-9]+", purpose)
    for start in range(len(tokens)):
        joined = ""; short = 0
        for token in tokens[start:start + 32]:
            joined += token
            short += int(len(token) <= 2)
            if len(joined) > len(target): break
            if len(joined) == len(target) and short >= 2 and joined.lower() == target:
                return True
    return False


def normalize_purpose(path: Path, root: Path) -> bool:
    rel = path.relative_to(root).as_posix()
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    razor = path.suffix.lower() == ".razor"
    pattern = re.compile(r"^\s*@\*\s*Purpose\s*:\s*(.*?)\s*\*@\s*$" if razor else r"^\s*(?://|#)\s*Purpose\s*:\s*(.*?)\s*$")
    indexes = [i for i, line in enumerate(lines[:80]) if pattern.match(line)]
    replacement = purpose_for(path, rel)
    prefix = f"@* Purpose: {replacement} *@" if razor else (f"# Purpose: {replacement}" if path.suffix.lower() in {'.ps1','.py'} else f"// Purpose: {replacement}")
    changed = False
    if not indexes:
        insert = 1 if path.suffix.lower() == ".ps1" and lines and lines[0].lstrip().lower().startswith("#requires") else 0
        lines.insert(insert, prefix); changed = True
    else:
        m = pattern.match(lines[indexes[0]])
        current = (m.group(1) if m else "").strip()
        if len(current) < 12 or len(current) > 180 or fragmented(current, path.stem):
            lines[indexes[0]] = prefix; changed = True
        for i in reversed(indexes[1:]):
            lines.pop(i); changed = True
    if changed:
        path.write_text("\n".join(lines).rstrip("\n") + "\n", encoding="utf-8")
    return changed


def split_semicolons(line: str) -> list[str]:
    if len(line) < 150 or line.count(";") < 2:
        return [line]
    indent = re.match(r"\s*", line).group(0)
    out=[]; buf=[]; quote=None; escape=False; paren=bracket=0; i=0; line_comment=False
    while i < len(line):
        ch=line[i]; nxt=line[i+1] if i+1 < len(line) else ''
        if line_comment:
            buf.append(line[i:]); i=len(line); break
        if quote:
            buf.append(ch)
            if escape: escape=False
            elif ch=='\\' and quote != "@\"": escape=True
            elif quote=='"' and ch=='"': quote=None
            elif quote=="'" and ch=="'": quote=None
            i+=1; continue
        if ch=='/' and nxt=='/': line_comment=True; continue
        if ch=='"': quote='"'; buf.append(ch); i+=1; continue
        if ch=="'": quote="'"; buf.append(ch); i+=1; continue
        if ch=='(': paren+=1
        elif ch==')': paren=max(0,paren-1)
        elif ch=='[': bracket+=1
        elif ch==']': bracket=max(0,bracket-1)
        buf.append(ch)
        if ch==';' and paren==0 and bracket==0:
            segment=''.join(buf).rstrip()
            if segment: out.append(segment)
            buf=[indent]
        i+=1
    tail=''.join(buf).rstrip()
    if tail.strip(): out.append(tail)
    return out if len(out)>1 else [line]


def format_dense_csharp(path: Path) -> bool:
    text=path.read_text(encoding='utf-8-sig',errors='replace')
    lines=text.replace('\r\n','\n').replace('\r','\n').split('\n')
    new=[]
    for line in lines:
        new.extend(split_semicolons(line))
    rendered='\n'.join(new)
    changed=rendered!=text.replace('\r\n','\n').replace('\r','\n')
    if changed: path.write_text(rendered.rstrip('\n')+'\n',encoding='utf-8')
    return changed


def method_name(line: str) -> str | None:
    s=line.strip()
    if not re.match(r"^(public|private|protected|internal)\b", s) or "(" not in s:
        return None
    # Exclude declarations that are plainly delegates/properties.
    prefix=s.split('(',1)[0]
    parts=re.findall(r"[A-Za-z_][A-Za-z0-9_]*", prefix)
    if not parts: return None
    return parts[-1]


def brace_delta(line: str) -> int:
    # Good enough for source organization: remove quoted strings/chars and line comments before counting braces.
    stripped=re.sub(r'@"(?:""|[^"])*"', '""', line)
    stripped=re.sub(r'"(?:\\.|[^"\\])*"', '""', stripped)
    stripped=re.sub(r"'(?:\\.|[^'\\])*'", "''", stripped)
    stripped=stripped.split('//',1)[0]
    return stripped.count('{')-stripped.count('}')


def split_partial_methods(path: Path, groups: dict[str, tuple[str, ...]]) -> list[Path]:
    text=path.read_text(encoding='utf-8-sig',errors='replace')
    class_match=re.search(r"public\s+(sealed\s+)?class\s+(\w+)\s*(?:\n\s*)?\{", text)
    if not class_match:
        # Avoid unsafe primary-constructor conversion; formatting still applies.
        return []
    name=class_match.group(2)
    text=text[:class_match.start()] + class_match.group(0).replace('class '+name,'partial class '+name) + text[class_match.end():]
    lines=text.splitlines()
    # Locate declaration line and class depth.
    start=next((i for i,l in enumerate(lines) if re.search(rf"\bpartial\s+class\s+{re.escape(name)}\b",l)),None)
    if start is None: return []
    depth=0; in_class=False; spans=[]; i=start
    while i < len(lines):
        d=brace_delta(lines[i]);
        if not in_class:
            depth += d
            if '{' in lines[i]: in_class=True
            i+=1; continue
        if depth==1:
            mname=method_name(lines[i])
            if mname and mname != name:
                mstart=i; local=brace_delta(lines[i]); j=i
                if '=>' in lines[i] and ';' in lines[i] and local==0:
                    spans.append((mstart,i,mname)); i+=1; continue
                while local<=0 and j+1<len(lines):
                    j+=1; local+=brace_delta(lines[j])
                    if ';' in lines[j] and local==0 and '{' not in ''.join(lines[mstart:j+1]): break
                while local>0 and j+1<len(lines):
                    j+=1; local+=brace_delta(lines[j])
                spans.append((mstart,j,mname)); i=j+1; continue
        depth += d; i+=1
    assignments={}
    for s,e,n in spans:
        dest=None
        for group,keys in groups.items():
            if any(k.lower() in n.lower() for k in keys): dest=group; break
        if dest is None and n.startswith(('Parse','Read','Clean','Need','Normalize','Is','Try','GetString','Merge','Map','Clip','F','D')):
            dest='Helpers'
        if dest: assignments[(s,e,n)]=dest
    if not assignments:
        path.write_text(text.rstrip()+"\n",encoding='utf-8'); return []
    usings='\n'.join(l for l in lines[:start] if l.startswith('using '))
    namespace=next((l for l in lines[:start] if l.startswith('namespace ')),None)
    grouped={}
    for (s,e,n),g in assignments.items(): grouped.setdefault(g,[]).append('\n'.join(lines[s:e+1]))
    remove=set()
    for s,e,n in assignments: remove.update(range(s,e+1))
    core='\n'.join(l for idx,l in enumerate(lines) if idx not in remove)
    path.write_text(re.sub(r'\n{3,}','\n\n',core).rstrip()+"\n",encoding='utf-8')
    created=[]
    for group,members in grouped.items():
        dest=path.with_name(f"{path.stem}.{group}.cs")
        purpose=f"// Purpose: Organizes the {group} responsibility of {name} without changing its public contract."
        content=purpose+'\n'+usings+'\n\n'+(namespace or '')+'\n\n'+f"public sealed partial class {name}\n{{\n"+'\n\n'.join(members)+'\n}\n'
        dest.write_text(content,encoding='utf-8'); created.append(dest)
    return created


def harden_empty_catches(path: Path) -> bool:
    text=path.read_text(encoding='utf-8-sig',errors='replace')
    pattern=re.compile(r"catch\s*\{\s*\}")
    if not pattern.search(text): return False
    repl="catch (Exception exception) when (IsExpectedBestEffortFailure(exception))\n            {\n                _ = exception; // Expected best-effort cleanup/parsing failure; unexpected exceptions remain visible.\n            }"
    text=pattern.sub(repl,text)
    if 'IsExpectedBestEffortFailure(Exception exception)' not in text:
        helper="""\n    private static bool IsExpectedBestEffortFailure(Exception exception) =>\n        exception is IOException\n        or UnauthorizedAccessException\n        or InvalidOperationException\n        or JsonException\n        or FormatException\n        or OperationCanceledException\n        or System.ComponentModel.Win32Exception;\n"""
        idx=text.rfind('}')
        text=text[:idx]+helper+text[idx:]
    path.write_text(text.rstrip()+"\n",encoding='utf-8'); return True


def find_matching_brace(text: str, open_index: int) -> int:
    depth=0; quote=None; escape=False; line_comment=False; block_comment=False; i=open_index
    while i<len(text):
        ch=text[i]; nxt=text[i+1] if i+1<len(text) else ''
        if line_comment:
            if ch=='\n': line_comment=False
            i+=1; continue
        if block_comment:
            if ch=='*' and nxt=='/': block_comment=False; i+=2; continue
            i+=1; continue
        if quote:
            if escape: escape=False
            elif ch=='\\' and quote in {'"',"'"}: escape=True
            elif ch==quote: quote=None
            i+=1; continue
        if ch=='/' and nxt=='/': line_comment=True; i+=2; continue
        if ch=='/' and nxt=='*': block_comment=True; i+=2; continue
        if ch in {'"',"'"}: quote=ch; i+=1; continue
        if ch=='{': depth+=1
        elif ch=='}':
            depth-=1
            if depth==0: return i
        i+=1
    raise ValueError('unmatched brace')


def razor_namespace(rel: str) -> str:
    sub=Path(rel).relative_to('src/Dip.Web').parent.parts
    return '.'.join(('Dip','Web',*sub))


def extract_razor_codebehind(path: Path, root: Path) -> Path | None:
    rel=path.relative_to(root).as_posix(); text=path.read_text(encoding='utf-8-sig',errors='replace')
    if '@code' not in text: return None
    m=list(re.finditer(r'@code\s*\{',text))
    if not m: return None
    match=m[-1]; open_index=text.find('{',match.start()); close=find_matching_brace(text,open_index)
    body=text[open_index+1:close].strip('\n')
    text=text[:match.start()].rstrip()+"\n"
    path.write_text(text,encoding='utf-8')
    usings=[]
    for line in text.splitlines():
        if line.lstrip().startswith('@using '): usings.append('using '+line.split('@using ',1)[1].strip().rstrip(';')+';')
    common=['using Microsoft.AspNetCore.Components;','using Microsoft.AspNetCore.Components.Forms;','using Microsoft.AspNetCore.Components.Web;']
    all_usings=[]
    for u in common+usings:
        if u not in all_usings: all_usings.append(u)
    cls=path.stem; ns=razor_namespace(rel)
    dest=path.with_suffix('.razor.cs')
    code='// Purpose: Holds state and event-handling logic for the '+cls+' Razor component.\n'+'\n'.join(all_usings)+f'\n\nnamespace {ns};\n\npublic partial class {cls}\n{{\n'+body+'\n}\n'
    dest.write_text(code,encoding='utf-8'); return dest


def format_razor_markup(path: Path) -> bool:
    text=path.read_text(encoding='utf-8-sig',errors='replace'); lines=text.splitlines(); out=[]; changed=False
    for line in lines:
        if len(line)<260:
            out.append(line); continue
        indent=re.match(r'\s*',line).group(0)
        parts=re.split(r'(?<=>)(?=<)|(?<=\})(?=@(?:if|foreach|for|while)\b)',line.strip())
        if len(parts)>1:
            out.extend(indent+p.strip() for p in parts if p.strip()); changed=True
        else: out.append(line)
    if changed: path.write_text('\n'.join(out).rstrip()+"\n",encoding='utf-8')
    return changed


def verify(root: Path) -> list[str]:
    issues=[]
    for root_name in ROOTS:
        base=root/root_name
        if not base.exists(): continue
        for path in base.rglob('*'):
            if not path.is_file() or path.suffix.lower() not in SOURCE_EXTENSIONS or any(part in EXCLUDED_PARTS for part in path.parts): continue
            text=path.read_text(encoding='utf-8-sig',errors='replace'); rel=path.relative_to(root).as_posix()
            if path.suffix.lower()=='.razor':
                m=re.search(r'(?m)^\s*@\*\s*Purpose\s*:\s*(.+?)\s*\*@\s*$',text)
            else: m=re.search(r'(?m)^\s*(?://|#)\s*Purpose\s*:\s*(.+?)\s*$',text)
            if not m: issues.append(f'MISSING_PURPOSE {rel}'); continue
            purpose=m.group(1)
            if fragmented(purpose,path.stem): issues.append(f'BROKEN_PURPOSE {rel}')
    for rel in TARGET_CSHARP_FORMAT:
        p=root/rel
        if not p.exists(): continue
        for i,line in enumerate(p.read_text(encoding='utf-8-sig',errors='replace').splitlines(),1):
            if len(line)>=180 and line.count(';')>=3: issues.append(f'COMPRESSED_CSHARP {rel}:{i}')
    p=root/'src/Dip.Infrastructure/QualityAutomation/ProcessExecutor.cs'
    if p.exists() and re.search(r'catch\s*\{\s*\}',p.read_text(encoding='utf-8-sig',errors='replace')):
        issues.append('EMPTY_CATCH ProcessExecutor.cs')
    return issues


def apply(root: Path) -> None:
    changed=[]
    for root_name in ROOTS:
        base=root/root_name
        if not base.exists(): continue
        for path in base.rglob('*'):
            if path.is_file() and path.suffix.lower() in SOURCE_EXTENSIONS and not any(part in EXCLUDED_PARTS for part in path.parts):
                if normalize_purpose(path,root): changed.append(path.relative_to(root).as_posix())
    created=[]
    for rel in TARGET_CSHARP_FORMAT:
        p=root/rel
        if p.exists():
            format_dense_csharp(p)
            if rel in CSHARP_SPLITS: created.extend(split_partial_methods(p,CSHARP_SPLITS[rel]))
    for rel in ("src/Dip.Infrastructure/QualityAutomation/ProcessExecutor.cs","src/Dip.Application/Features/Management/ManagementOperationsService.cs"):
        p=root/rel
        if p.exists(): harden_empty_catches(p); format_dense_csharp(p)
    for rel in RAZOR_CODEBEHIND:
        p=root/rel
        if p.exists():
            dest=extract_razor_codebehind(p,root)
            if dest: created.append(dest)
    for rel in RAZOR_FORMAT:
        p=root/rel
        if p.exists(): format_razor_markup(p)
    # Run Purpose normalization again for newly-created partial/code-behind files.
    for p in created:
        if p.exists(): normalize_purpose(p,root)
    print(f"Applied requested source refactor. Purpose-normalized files: {len(set(changed))}; created partial/code-behind files: {len(created)}")


def main() -> int:
    ap=argparse.ArgumentParser(); ap.add_argument('--repository-root',default='.'); ap.add_argument('--verify-only',action='store_true'); args=ap.parse_args()
    root=Path(args.repository_root).resolve()
    if not args.verify_only: apply(root)
    issues=verify(root)
    if issues:
        print('\n'.join(issues[:200])); raise SystemExit(f"Requested source refactor verification failed with {len(issues)} finding(s).")
    print('PASS: requested source refactor verification completed.')
    return 0

if __name__=='__main__': raise SystemExit(main())
