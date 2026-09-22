#!/usr/bin/env python3
# Purpose: Provides repository tooling used by development and verification workflows.
from __future__ import annotations
import argparse, json, subprocess, sys
from pathlib import Path

REQUIRED=[
 'README.md','docs/09-portfolio/architecture.svg','docs/09-portfolio/ARCHITECTURE_OVERVIEW.md',
 'docs/09-portfolio/demo/dip-portfolio-demo.gif',
 'docs/09-portfolio/screenshots/project-intelligence.png','docs/09-portfolio/screenshots/qa-defect.png','docs/09-portfolio/screenshots/management-operations.png',
 'docs/09-portfolio/evidence/verified-baseline/benchmark-results.json',
 'docs/09-portfolio/evidence/verified-baseline/qa-bug-zoo-results.json',
 'docs/09-portfolio/evidence/verified-baseline/WINDOWS_DOTNET_RUNTIME_PASS.json',
 'src/Dip.Web/Pages/Dashboard/ManagementOperations.razor.cs',
 'src/Dip.Web/Components/Management/ManagementCockpitOverview.razor',
 'src/Dip.Web/Components/Management/ManagementSectionTabs.razor',
]
def main():
 ap=argparse.ArgumentParser(); ap.add_argument('--root',default='.'); ap.add_argument('--output',default='')
 a=ap.parse_args(); root=Path(a.root).resolve(); checks=[]
 def add(name,ok,detail): checks.append({'name':name,'status':'PASS' if ok else 'FAIL','detail':detail})
 missing=[x for x in REQUIRED if not (root/x).is_file()]
 add('required-portfolio-files',not missing,'all required files present' if not missing else 'missing: '+', '.join(missing))
 readme=root/'README.md'; size=readme.stat().st_size if readme.exists() else 0
 add('readme-size',0<size<=12000,f'{size} bytes (target <= 12000)')
 empties=[p.relative_to(root).as_posix() for p in (root/'src/Dip.Web').rglob('*.razor') if p.stat().st_size==0]
 add('no-empty-razor',not empties,'none' if not empties else ', '.join(empties))
 monolith=root/'src/Dip.Web/Pages/Dashboard/ManagementOperations.razor'
 msize=monolith.stat().st_size if monolith.exists() else 0
 add('management-component-split',msize<36000,f'ManagementOperations.razor={msize} bytes; code-behind + reusable components required')
 # Validate evidence values without changing them.
 try:
  b=json.loads((root/'docs/09-portfolio/evidence/verified-baseline/benchmark-results.json').read_text())
  ok=b['measurements']['projectList']['p95Ms']==4.174 and b['measurements']['hundredRps']['failures']==0 and b['measurements']['millionMetricQuery']['rowsInDataset']==1_000_000
  add('benchmark-evidence-shape',ok,f"commit={b.get('provenance',{}).get('commit','?')}")
 except Exception as e: add('benchmark-evidence-shape',False,str(e))
 try:
  q=json.loads((root/'docs/09-portfolio/evidence/verified-baseline/qa-bug-zoo-results.json').read_text())
  ok=q['injectedBugCases']==19 and q['detectedBugCases']==19 and q['falsePositiveCases']==0 and q['reproducedCases']==9
  add('qa-evidence-shape',ok,f"recall={q.get('detectionRecallPercent')} falsePositive={q.get('falsePositiveRatePercent')} reproduction={q.get('reproductionRatePercent')}")
 except Exception as e: add('qa-evidence-shape',False,str(e))
 result={'schemaVersion':'1.0','status':'PASS' if all(c['status']=='PASS' for c in checks) else 'FAIL','checks':checks}
 payload=json.dumps(result,indent=2,ensure_ascii=False)
 if a.output:
  out=Path(a.output); out.parent.mkdir(parents=True,exist_ok=True); out.write_text(payload+'\n',encoding='utf-8')
 print(payload); return 0 if result['status']=='PASS' else 2
if __name__=='__main__': raise SystemExit(main())
