#!/usr/bin/env python3
from __future__ import annotations
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
ROOTS=("src","tests","scripts","tools","services","collectors","plugins")
EXTS={".cs",".razor",".ps1",".py",".cpp",".cc",".c",".h",".hpp",".js",".mjs",".ts",".tsx"}
SHORT_CHAIN=re.compile(r"\b[A-Za-z]\s+[A-Za-z]{1,2}\s+[A-Za-z]{1,2}\b")
PURPOSE=re.compile(r"Purpose:\s*(.*?)(?:\s*\*/|\s*\*@)?\s*$")
ACRONYMS={"Qa":"QA","Gui":"GUI","Ui":"UI","Api":"API","Ci":"CI","Ide":"IDE","Id":"ID","Dto":"DTO","Jwt":"JWT","Sql":"SQL","Db":"DB","Http":"HTTP","Https":"HTTPS","Github":"GitHub","Cpp":"C++","P2":"P2","Dip":"DIP"}
SUFFIXES=("Controller","Service","Processor","Evaluator","Planner","Handler","Engine","Resolver","Worker")

def words(name:str)->str:
    name=re.sub(r"\.[^.]+$","",name)
    name=name.replace("-"," ").replace("_"," ")
    name=re.sub(r"([a-z0-9])([A-Z])",r"\1 \2",name)
    name=re.sub(r"([A-Z]+)([A-Z][a-z])",r"\1 \2",name)
    name=re.sub(r"\s+"," ",name).strip()
    for a,b in ACRONYMS.items():
        name=re.sub(rf"\b{re.escape(a)}\b",b,name)
    name=name.replace("Git Hub","GitHub").replace("C Sharp","C#")
    return name

def area(path:Path)->str:
    p=path.as_posix()
    m=re.search(r"/Features/([^/]+)",p)
    if m: return words(m.group(1)).lower()
    for marker in ("Infrastructure","Web","Worker","Agent.Core","Agent.Windows","Shared"):
        token=f"/Dip.{marker}/"
        if token in p:
            tail=p.split(token,1)[1].split("/",1)[0]
            return words(tail).lower() if tail else marker.lower()
    return "this repository area"

def malformed(line:str)->bool:
    if "Purpose:" not in line: return False
    m=PURPOSE.search(line)
    if not m: return False
    body=m.group(1)
    tokens=re.findall(r"\b[A-Za-z]+\b",body)
    short=[t for t in tokens if len(t)<=2]
    return bool(SHORT_CHAIN.search(body) or (len(short)>=5 and len(short)>=max(5,len(tokens)//3)))

def purpose(path:Path)->str:
    p=path.as_posix()
    title=words(path.name)
    ar=area(path)
    if p.startswith("tests/"):
        body=f"Verifies {re.sub(r' Tests$','',title)} behavior, edge cases, and invariants for {ar}."
    elif p.startswith("scripts/"):
        body=f"Automates {title} for repository development and verification workflows."
    elif p.startswith("tools/"):
        body=f"Provides {title} tooling for repository verification and development workflows."
    elif "/Controllers/" in p and path.suffix==".cs":
        body=f"Exposes {re.sub(r' Controller$','',title)} HTTP endpoints for the DIP API."
    elif "/Dip.Application/" in p:
        body=f"Coordinates {re.sub(r' (?:'+'|'.join(SUFFIXES)+r')$','',title)} application responsibilities for {ar}."
    elif "/Dip.Domain/" in p:
        body=f"Defines {title} domain behavior and invariants for {ar}."
    elif "/Dip.Infrastructure/" in p:
        body=f"Implements {title} infrastructure responsibilities for {ar}."
    elif "/Dip.Worker/" in p:
        body=f"Runs {re.sub(r' Worker$','',title)} background processing for DIP."
    elif "/Dip.Web/" in p and path.suffix==".razor":
        body=f"Renders the {title} UI and coordinates its {ar} interactions."
    elif "/Dip.Web/" in p:
        body=f"Implements {title} web behavior for {ar}."
    elif "/Dip.Agent." in p:
        body=f"Implements {title} endpoint-agent responsibilities for {ar}."
    elif p.startswith("services/"):
        body=f"Implements {title} service responsibilities for {ar}."
    elif p.startswith("collectors/"):
        body=f"Implements {title} collector responsibilities for {ar}."
    elif p.startswith("plugins/"):
        body=f"Implements {title} plugin responsibilities for {ar}."
    else:
        body=f"Defines {title} responsibilities for {ar}."
    if path.suffix in {".ps1",".py"}: return f"# Purpose: {body}"
    if path.suffix==".razor": return f"@* Purpose: {body} *@"
    return f"// Purpose: {body}"

changed=[]
for root_name in ROOTS:
    root=ROOT/root_name
    if not root.exists(): continue
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in EXTS: continue
        try: text=path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError: continue
        lines=text.splitlines()
        idx=next((i for i,l in enumerate(lines[:12]) if "Purpose:" in l),None)
        if idx is None or not malformed(lines[idx]): continue
        lines[idx]=purpose(path)
        ending="\n" if text.endswith(("\n","\r\n")) else ""
        path.write_text("\n".join(lines)+ending,encoding="utf-8")
        changed.append(path.relative_to(ROOT).as_posix())

print(f"Repaired {len(changed)} malformed Purpose comments.")
for p in changed: print(p)

remaining=[]
for root_name in ROOTS:
    root=ROOT/root_name
    if not root.exists(): continue
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in EXTS: continue
        try: text=path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError: continue
        for line in text.splitlines()[:12]:
            if "Purpose:" in line and malformed(line):
                remaining.append(path.relative_to(ROOT).as_posix()); break
if remaining:
    print("Residual malformed Purpose comments:")
    for p in remaining: print(p)
    raise SystemExit(2)
