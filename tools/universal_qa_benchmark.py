#!/usr/bin/env python3
# Purpose: Implements u ni ve rs al q a b en ch ma rk tooling used by repository verification and development workflows.
"""Measured cross-platform toolchain benchmark for DIP Universal QA.

This bootstrap benchmark measures detection/reproduction mechanics without
pretending to measure root-cause ranking that it does not exercise. Unsupported
toolchains are reported as skipped, never passed.
"""
from __future__ import annotations
import argparse, hashlib, json, os, platform, shutil, subprocess, tempfile, time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from statistics import mean

@dataclass
class Case:
    language:str; tool:str; clean_name:str; clean_src:str; bad_name:str; bad_src:str; cmd:callable; extras:dict|None=None

def which(x): return shutil.which(x)
def run(cmd,cwd,timeout=30):
    t=time.perf_counter()
    try:
        p=subprocess.run(cmd,cwd=cwd,text=True,capture_output=True,timeout=timeout,env={**os.environ,"NO_COLOR":"1"})
        return p.returncode,(time.perf_counter()-t)*1000,(p.stdout+p.stderr)[-4000:]
    except subprocess.TimeoutExpired:
        return 124,(time.perf_counter()-t)*1000,"timeout"

def powershell_parse_cmd(file_name):
    escaped=file_name.replace("'","''")
    script=("$errors=$null;$tokens=$null;"+f"[System.Management.Automation.Language.Parser]::ParseFile('{escaped}',[ref]$tokens,[ref]$errors)|Out-Null;"+"if($errors.Count -gt 0){exit 1};exit 0")
    return [which("pwsh"),"-NoProfile","-NonInteractive","-Command",script]

def cases():
    return [
      Case("python","python","app.py","x=1\nprint(x)\n","app.py","def broken(:\n pass\n",lambda f:[which("python3") or which("python"),"-m","py_compile",f]),
      Case("javascript","node","app.js","const x=1; console.log(x);\n","app.js","function broken( {\n",lambda f:[which("node"),"--check",f]),
      Case("typescript","tsc","app.ts","const x: number = 1;\n","app.ts","const x: number = ;\n",lambda f:[which("tsc"),"--noEmit","--pretty","false","--target","ES2020",f]),
      Case("go","go","main.go","package main\nfunc main(){}\n","main.go","package main\nfunc main( {\n",lambda f:[which("go"),"test","./..."],{"go.mod":"module dipbench\ngo 1.22\n"}),
      Case("java","javac","Main.java","class Main { public static void main(String[] a){} }\n","Main.java","class Main { void broken( { }\n",lambda f:[which("javac"),f]),
      Case("kotlin","kotlinc","Main.kt","fun main(){ println(1) }\n","Main.kt","fun main( {\n",lambda f:[which("kotlinc"),f,"-d","out.jar"]),
      Case("ruby","ruby","app.rb","x=1\nputs x\n","app.rb","def broken(\nend\n",lambda f:[which("ruby"),"-c",f]),
      Case("php","php","app.php","<?php $x=1; echo $x; ?>\n","app.php","<?php function broken( { ?>\n",lambda f:[which("php"),"-l",f]),
      Case("bash","bash","app.sh","#!/usr/bin/env bash\necho ok\n","app.sh","#!/usr/bin/env bash\nif then\n",lambda f:[which("bash"),"-n",f]),
      Case("swift","swiftc","main.swift","let x:Int=1\nprint(x)\n","main.swift","let x:Int =\n",lambda f:[which("swiftc"),"-typecheck",f]),
      Case("objectivec","clang","main.m","int main(void){return 0;}\n","main.m","int main( { return 0; }\n",lambda f:[which("clang"),"-fsyntax-only",f]),
      Case("c","clang","main.c","int main(void){return 0;}\n","main.c","int main( { return 0; }\n",lambda f:[which("clang"),"-fsyntax-only","-Wall","-Wextra",f]),
      Case("cpp","clang++","main.cpp","int main(){return 0;}\n","main.cpp","int main( { return 0; }\n",lambda f:[which("clang++"),"-fsyntax-only","-Wall","-Wextra",f]),
      Case("lua","luac","app.lua","local x=1\nprint(x)\n","app.lua","function broken(\n",lambda f:[which("luac"),"-p",f]),
      Case("powershell","pwsh","app.ps1","$x = 1\nWrite-Output $x\n","app.ps1","function Broken( {\n",powershell_parse_cmd),
      Case("csharp","dotnet","Program.cs","class Program { static void Main(){} }\n","Program.cs","class Program { static void Main( { }\n",lambda f:[which("dotnet"),"build","--nologo"],{"bench.csproj":"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>"}),
      Case("rust","rustc","main.rs","fn main() {}\n","main.rs","fn main( {\n",lambda f:[which("rustc"),f]),
      Case("sql","sqlfluff","app.sql","SELECT 1;\n","app.sql","SELEC FROM;\n",lambda f:[which("sqlfluff"),"lint",f]),
      Case("glsl","glslangValidator","main.vert","#version 450\nvoid main(){gl_Position=vec4(0.0);}\n","main.vert","#version 450\nvoid main( {\n",lambda f:[which("glslangValidator"),f]),
      Case("gdscript","godot","script.gd","extends Node\nfunc _ready():\n    pass\n","script.gd","extends Node\nfunc broken(\n",lambda f:[which("godot") or which("godot4"),"--headless","--editor","--quit"],{"project.godot":"[application]\nconfig/name=\"DIPBench\"\n[rendering]\nrenderer/rendering_method=\"gl_compatibility\"\n"}),
    ]

def version(tool):
    exe=which(tool)
    if not exe:return None
    for args in (["--version"],["-version"],["-v"]):
      try:
        p=subprocess.run([exe,*args],text=True,capture_output=True,timeout=5);text=(p.stdout+p.stderr).strip().splitlines()
        if text:return text[0][:200]
      except Exception:pass
    return exe

def pct(n,d): return round(100*n/d,2) if d else 0.0

def main():
    ap=argparse.ArgumentParser();ap.add_argument("--output",default="artifacts/universal-qa-benchmark");a=ap.parse_args();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True);results=[]
    for c in cases():
      exe=which(c.tool)
      if not exe:
        results.append({"language":c.language,"tool":c.tool,"status":"skipped","reason":"tool unavailable"});continue
      row={"language":c.language,"tool":c.tool,"toolVersion":version(c.tool),"status":"executed"}
      with tempfile.TemporaryDirectory(prefix="dip-uqa-") as td:
        td=Path(td)
        for n,v in (c.extras or {}).items():(td/n).write_text(v,encoding="utf-8")
        clean=td/c.clean_name;clean.write_text(c.clean_src,encoding="utf-8")
        rc0,ms0,_=run(c.cmd(clean.name),td);clean.unlink();bad=td/c.bad_name;bad.write_text(c.bad_src,encoding="utf-8")
        rc1,ms1,log1=run(c.cmd(bad.name),td);rc2,ms2,_=run(c.cmd(bad.name),td)
      row.update(cleanExitCode=rc0,defectExitCode=rc1,cleanPassed=rc0==0,defectDetected=rc1!=0,reproduced=(rc1!=0 and rc2!=0),cleanDurationMs=round(ms0,2),defectDetectionDurationMs=round(ms1,2),reproductionDurationMs=round(ms2,2),durationMs=round(ms0+ms1+ms2,2),diagnosticPreview=log1[-800:]);results.append(row)

    executed=[r for r in results if r["status"]=="executed"]
    tp=sum(r["defectDetected"] for r in executed); fn=len(executed)-tp
    fp=sum(not r["cleanPassed"] for r in executed); tn=len(executed)-fp
    reproduced=sum(r["reproduced"] for r in executed)
    precision=pct(tp,tp+fp); recall=pct(tp,tp+fn); fpr=pct(fp,fp+tn); fnr=pct(fn,fn+tp)
    detection_times=[r["defectDetectionDurationMs"] for r in executed]; reproduction_times=[r["reproductionDurationMs"] for r in executed if r["defectDetected"]]
    slices=[{"language":r["language"],"detected":r["defectDetected"],"cleanPassed":r["cleanPassed"],"reproduced":r["reproduced"],"detectionMs":r["defectDetectionDurationMs"],"reproductionMs":r["reproductionDurationMs"]} for r in executed]
    payload={
      "schemaVersion":"2.0","benchmark":"DIP Universal QA Toolchain Benchmark","evidenceClass":"measured-native-toolchain-bootstrap","generatedAt":datetime.now(timezone.utc).isoformat(),
      "environment":{"platform":platform.platform(),"python":platform.python_version()},
      "executedLanguages":[r["language"] for r in executed],"skippedLanguages":[r["language"] for r in results if r["status"]=="skipped"],
      "cases":len(executed),"confusionMatrix":{"truePositive":tp,"falsePositive":fp,"trueNegative":tn,"falseNegative":fn},
      "precisionPercent":precision,"recallPercent":recall,"falsePositiveRatePercent":fpr,"falseNegativeRatePercent":fnr,
      "reproductionRatePercent":pct(reproduced,len(executed)),
      "meanDetectionTimeMs":round(mean(detection_times),2) if detection_times else None,"meanReproductionTimeMs":round(mean(reproduction_times),2) if reproduction_times else None,
      "rootCauseTop1Accuracy":"not-measured-by-bootstrap","rootCauseTop3Accuracy":"not-measured-by-bootstrap",
      "formalAdapterBenchmark":False,"note":"Bootstrap measures native-toolchain defect detection/reproduction only. Root-cause ranking requires labeled DIP adapter/diagnostic cases and is intentionally not inferred here.",
      "domainSlices":slices,"results":results}
    raw=json.dumps(payload,indent=2,ensure_ascii=False);(out/"universal-qa-benchmark-results.json").write_text(raw,encoding="utf-8");sha=hashlib.sha256(raw.encode()).hexdigest();(out/"universal-qa-benchmark-results.sha256").write_text(sha+"  universal-qa-benchmark-results.json\n",encoding="ascii")
    md=(f"# DIP Universal QA Toolchain Benchmark\n\n- Evidence: **measured native toolchain bootstrap**\n- Executed languages: **{len(executed)}** ({', '.join(payload['executedLanguages']) or 'none'})\n- Precision: **{precision}%**\n- Recall: **{recall}%**\n- False-positive rate: **{fpr}%**\n- False-negative rate: **{fnr}%**\n- Reproduction rate: **{payload['reproductionRatePercent']}%**\n- Mean detection time: **{payload['meanDetectionTimeMs']} ms**\n- Mean reproduction time: **{payload['meanReproductionTimeMs']} ms**\n- Root-cause Top-1 / Top-3: **not measured by this bootstrap**\n- Skipped (tool unavailable): {', '.join(payload['skippedLanguages']) or 'none'}\n\nSkipped toolchains are never upgraded to PASS. Formal adapter/root-cause verification requires labeled DIP cases on the exact revision.\n")
    (out/"universal-qa-benchmark-results.md").write_text(md,encoding="utf-8");print(md)
    return 0 if executed and fp==0 and fn==0 and reproduced==len(executed) else 1

if __name__=="__main__":raise SystemExit(main())
