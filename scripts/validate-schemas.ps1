param([string]$SampleRoot="samples")
$ErrorActionPreference="Stop"
$python="services/python-analysis/.venv/Scripts/python.exe"; if(-not(Test-Path $python)){$python="python"}
$code=@'
import json, pathlib, sys
import jsonschema
schema=json.loads(pathlib.Path("schemas/collector-report.schema.json").read_text(encoding="utf-8"))
failed=False
for p in sorted(pathlib.Path(sys.argv[1]).glob("**/*report.json")):
    try: jsonschema.validate(json.loads(p.read_text(encoding="utf-8")),schema); print("[OK]",p)
    except jsonschema.ValidationError as e: failed=True; print("[NG]",p,e.message,file=sys.stderr)
sys.exit(1 if failed else 0)
'@
$tmp=New-TemporaryFile
try{Set-Content $tmp $code -Encoding utf8; & $python $tmp $SampleRoot}finally{Remove-Item $tmp -Force}
