#!/usr/bin/env python3
from __future__ import annotations
import argparse,csv,datetime as dt,json,re,sys,uuid
from pathlib import Path

VERSION="1.1.0"
ASSET_EXTS={".uasset",".umap",".ubulk",".uexp"}
SOURCE_EXTS={".cpp",".h",".hpp",".cs"}
ERROR_RE=re.compile(r"(?:error:|fatal error|ensure condition failed|assertion failed)",re.I)
WARNING_RE=re.compile(r"(?:warning:|log[^:]*: warning)",re.I)
BLUEPRINT_RE=re.compile(r"(?:LogBlueprint|BlueprintLog).*(?:Error|Compile failed)",re.I)
PACKAGE_RE=re.compile(r"(?:PackagingResults|AutomationTool).*(?:Error|BUILD FAILED|Cook failed)",re.I)
SHADER_RE=re.compile(r"(?:ShaderCompileWorker|LogShaderCompilers).*(?:Error|Failed)",re.I)
NETWORK_RE=re.compile(r"(?:NetDriver|LogNet).*(?:timeout|connection lost|failure)",re.I)

def utcnow(): return dt.datetime.now(dt.timezone.utc).isoformat().replace('+00:00','Z')
def metric(name,value,unit,**extra): return {"name":name,"value":value,"unit":unit,**{k:v for k,v in extra.items() if v is not None}}
def issue(category,severity,title,description,source,rule_id=None,evidence=None):
    x={"category":category,"severity":severity,"title":title[:200],"description":description[:4000],"source":source}
    if rule_id:x["ruleId"]=rule_id
    if evidence is not None:x["evidenceJson"]=json.dumps(evidence,separators=(',',':'))
    return x

def _collect_automation(report:Path,metrics:list,issues:list):
    try:
        data=json.loads(report.read_text(encoding='utf-8-sig'))
        stack=[data];passed=failed=skipped=0
        while stack:
            node=stack.pop()
            if isinstance(node,dict):
                state=str(node.get('state',node.get('State',node.get('result',node.get('Result',''))))).lower()
                if state in {'success','succeeded','pass','passed'}: passed+=1
                elif state in {'fail','failed','error'}: failed+=1
                elif state in {'notrun','not run','skipped','skip'}: skipped+=1
                stack.extend(node.values())
            elif isinstance(node,list): stack.extend(node)
        metrics += [metric('unreal.automation.passed',passed,'count'),metric('unreal.automation.failed',failed,'count'),metric('unreal.automation.skipped',skipped,'count')]
        if failed: issues.append(issue('AutomationTest','Critical','Unreal Automation Test failure',f'{failed} failed Automation Test result(s) were found.',str(report),'UNREAL_AUTOMATION_FAILED',{'failed':failed,'passed':passed,'skipped':skipped}))
    except Exception as exc:
        issues.append(issue('AutomationTest','Warning','Unable to parse Unreal Automation report',str(exc),str(report),'UNREAL_AUTOMATION_PARSE'))

def collect(root:Path,insights:Path|None,automation:Path|None=None):
    files=[p for p in root.rglob('*') if p.is_file()]
    uproject=next(iter(root.glob('*.uproject')),None)
    metrics=[];issues=[]
    assets=[p for p in files if p.suffix.lower() in ASSET_EXTS]
    sources=[p for p in files if p.suffix.lower() in SOURCE_EXTS]
    maps=[p for p in files if p.suffix.lower()=='.umap']
    configs=[p for p in files if p.suffix.lower()=='.ini' and 'Config' in p.parts]
    plugins=[p for p in files if p.suffix.lower()=='.uplugin']
    total_asset_bytes=sum(p.stat().st_size for p in assets)
    metrics += [metric('unreal.asset_count',len(assets),'count'),metric('unreal.asset_bytes',total_asset_bytes,'bytes'),metric('unreal.map_count',len(maps),'count'),metric('unreal.source_file_count',len(sources),'count'),metric('unreal.config_file_count',len(configs),'count'),metric('unreal.plugin_descriptor_count',len(plugins),'count')]
    rhi_configs=0
    for cfg in configs:
        try:
            txt=cfg.read_text(encoding='utf-8',errors='replace')
            if 'DefaultGraphicsRHI' in txt or 'TargetedRHIs' in txt: rhi_configs+=1
        except OSError: pass
    metrics.append(metric('unreal.default_rhi_config_count',rhi_configs,'count'))
    if rhi_configs==0: issues.append(issue('PlatformConfiguration','Warning','No explicit target RHI configuration observed','Review DefaultGraphicsRHI/TargetedRHIs for the release platforms; this is a review signal, not proof of a misconfiguration.',str(root),'UNREAL_RHI_REVIEW'))
    if uproject:
        try:
            data=json.loads(uproject.read_text(encoding='utf-8-sig'))
            metrics.append(metric('unreal.module_count',len(data.get('Modules',[])),'count'))
            metrics.append(metric('unreal.enabled_plugin_count',sum(1 for p in data.get('Plugins',[]) if p.get('Enabled',False)),'count'))
            engine=data.get('EngineAssociation')
            if engine: metrics.append(metric('unreal.engine_association',1,'state',benchmarkVersion=str(engine)))
        except Exception as exc:
            issues.append(issue('ProjectConfiguration','Warning','Unable to parse .uproject',str(exc),str(uproject),'UPROJECT_PARSE'))
    else:
        issues.append(issue('ProjectConfiguration','Critical','No .uproject found','The collector root does not contain an Unreal .uproject descriptor.',str(root),'UPROJECT_MISSING'))
    log_files=sorted((p for p in files if p.suffix.lower()=='.log' and ('Saved' in p.parts or 'Logs' in p.parts)),key=lambda p:p.stat().st_mtime,reverse=True)[:20]
    errors=warnings=0
    for log in log_files:
        try:
            for idx,line in enumerate(log.read_text(encoding='utf-8',errors='replace').splitlines(),1):
                if ERROR_RE.search(line):
                    errors+=1
                    category,title,rule='UnrealLog','Unreal error','UNREAL_LOG_ERROR'
                    if BLUEPRINT_RE.search(line): category,title,rule='Blueprint','Blueprint compile failure','UNREAL_BLUEPRINT_COMPILE'
                    elif PACKAGE_RE.search(line): category,title,rule='Packaging','Unreal packaging/cook failure','UNREAL_PACKAGING_FAILED'
                    elif SHADER_RE.search(line): category,title,rule='Shader','Unreal shader compile failure','UNREAL_SHADER_COMPILE'
                    elif NETWORK_RE.search(line): category,title,rule='Network','Unreal network/runtime failure','UNREAL_NETWORK_FAILURE'
                    if len(issues)<200: issues.append(issue(category,'Critical',title,line.strip(),str(log),rule,{'line':idx}))
                elif WARNING_RE.search(line):
                    warnings+=1
                    if len(issues)<200: issues.append(issue('UnrealLog','Warning','Unreal warning',line.strip(),str(log),'UNREAL_LOG_WARNING',{'line':idx}))
        except OSError as exc:
            issues.append(issue('Collector','Warning','Unable to read Unreal log',str(exc),str(log),'UNREAL_LOG_READ'))
    metrics += [metric('unreal.log_error_count',errors,'count'),metric('unreal.log_warning_count',warnings,'count')]
    crash_files=[p for p in files if 'Crashes' in p.parts and p.suffix.lower() in {'.dmp','.xml','.runtime-xml','.ue4crashcontext'}]
    metrics.append(metric('unreal.crash_artifact_count',len(crash_files),'count'))
    if crash_files:
        issues.append(issue('CrashArtifact','Warning','Unreal crash artifacts detected',f'{len(crash_files)} crash artifact(s) exist under Saved/Crashes. Correlate them with the current build before treating them as an active regression.',str(crash_files[0]),'UNREAL_CRASH_ARTIFACTS',{'count':len(crash_files),'examples':[str(x) for x in crash_files[:10]]}))
    automation_report=automation if automation and automation.is_file() else next(iter(root.glob('Saved/Automation/Reports/**/index.json')),None)
    if automation_report:_collect_automation(automation_report,metrics,issues)
    if insights and insights.is_file():
        try:
            with insights.open(newline='',encoding='utf-8-sig') as f:
                rows=list(csv.DictReader(f))
            numeric={}
            for row in rows:
                for k,v in row.items():
                    if not k or v is None: continue
                    try:numeric.setdefault(k,[]).append(float(v))
                    except ValueError:pass
            for key,values in numeric.items():
                if not values:continue
                low=key.lower()
                if 'frametime' in low or 'frame_time' in low: metrics.append(metric('unreal.frame_time_ms.avg',sum(values)/len(values),'ms'))
                elif 'gamethread' in low: metrics.append(metric('unreal.game_thread_ms.avg',sum(values)/len(values),'ms'))
                elif 'renderthread' in low: metrics.append(metric('unreal.render_thread_ms.avg',sum(values)/len(values),'ms'))
                elif low in {'gpu','gpums','gpu_ms'}: metrics.append(metric('unreal.gpu_ms.avg',sum(values)/len(values),'ms'))
                elif 'memory' in low: metrics.append(metric('unreal.memory.avg',sum(values)/len(values),'bytes'))
        except Exception as exc:
            issues.append(issue('Insights','Warning','Unable to parse Unreal Insights CSV',str(exc),str(insights),'UNREAL_INSIGHTS_PARSE'))
    return metrics,issues

def main(argv=None):
    p=argparse.ArgumentParser(description='DIP Unreal Engine collector')
    p.add_argument('--root',required=True);p.add_argument('--project-id',required=True);p.add_argument('--repository',required=True);p.add_argument('--commit-sha',required=True);p.add_argument('--branch',default='main');p.add_argument('--external-run-id',required=True);p.add_argument('--attempt-number',type=int,default=1);p.add_argument('--insights-csv');p.add_argument('--automation-report');p.add_argument('--output',required=True)
    a=p.parse_args(argv);root=Path(a.root).resolve();started=utcnow();metrics,issues=collect(root,Path(a.insights_csv).resolve() if a.insights_csv else None,Path(a.automation_report).resolve() if a.automation_report else None);finished=utcnow();status='Failed' if any(x['severity']=='Critical' and x.get('ruleId')=='UPROJECT_MISSING' for x in issues) else 'Succeeded'
    report={'schemaVersion':'1.0','reportId':str(uuid.uuid4()),'externalRunId':a.external_run_id,'attemptNumber':a.attempt_number,'collectorType':'unreal','collectorVersion':VERSION,'projectId':a.project_id,'repository':a.repository,'commitSha':a.commit_sha,'branch':a.branch,'startedAt':started,'finishedAt':finished,'status':status,'metrics':metrics,'issues':issues}
    out=Path(a.output);out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,indent=2),encoding='utf-8');return 0 if status=='Succeeded' else 2
if __name__=='__main__':sys.exit(main())
