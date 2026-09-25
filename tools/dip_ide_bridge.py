#!/usr/bin/env python3
# Purpose: Defines dip ide bridge responsibilities for this repository area.
"""LSP publishDiagnostics -> DIP defect-intelligence bridge.
Reads JSON/JSONL publishDiagnostics messages from stdin or --input and posts a
normalized batch. No source code is uploaded; only diagnostic fields are sent.
"""
from __future__ import annotations
import argparse,json,os,sys,urllib.request
from pathlib import Path
SEV={1:"Error",2:"Warning",3:"Information",4:"Hint"}
def messages(stream):
    text=stream.read()
    try:
        obj=json.loads(text);items=obj if isinstance(obj,list) else [obj]
    except json.JSONDecodeError:
        items=[json.loads(x) for x in text.splitlines() if x.strip()]
    for x in items: yield x
def main():
    p=argparse.ArgumentParser();p.add_argument('--url',default=os.getenv('DIP_API_URL','http://localhost:8080'));p.add_argument('--project-id',default=os.getenv('DIP_PROJECT_ID'));p.add_argument('--subject-key',default=os.getenv('DIP_SUBJECT_KEY','ide'));p.add_argument('--workspace',default='.');p.add_argument('--commit');p.add_argument('--language',default='unknown');p.add_argument('--token',default=os.getenv('DIP_TOKEN'));p.add_argument('--input');a=p.parse_args()
    if not a.project_id:raise SystemExit('--project-id or DIP_PROJECT_ID is required')
    stream=open(a.input,encoding='utf-8') if a.input else sys.stdin;out=[]
    for m in messages(stream):
        params=m.get('params',m);uri=params.get('uri','');path=uri[7:] if uri.startswith('file://') else uri
        for d in params.get('diagnostics',[]):
            start=d.get('range',{}).get('start',{});code=d.get('code','');source=d.get('source','lsp');out.append({'source':source,'code':str(code),'severity':SEV.get(d.get('severity'),str(d.get('severity','Information'))),'message':d.get('message',''),'filePath':path or None,'line':start.get('line',0)+1,'column':start.get('character',0)+1})
    body=json.dumps({'subjectKey':a.subject_key,'workspaceRelativePath':a.workspace,'commitSha':a.commit,'language':a.language,'diagnostics':out}).encode();url=f"{a.url.rstrip('/')}/api/v1/defect-intelligence/projects/{a.project_id}/ide-diagnostics";headers={'Content-Type':'application/json'}
    if a.token:headers['Authorization']='Bearer '+a.token
    req=urllib.request.Request(url,data=body,headers=headers,method='POST')
    with urllib.request.urlopen(req,timeout=30) as r:sys.stdout.write(r.read().decode())
if __name__=='__main__':main()
