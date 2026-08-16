[CmdletBinding()]param([string]$OutputDirectory="artifacts/universal-qa-benchmark")
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path;$python=(Get-Command python -ErrorAction SilentlyContinue);if(-not $python){$python=Get-Command python3 -ErrorAction SilentlyContinue};if(-not $python){throw 'Python 3 is required for the cross-platform Universal QA toolchain benchmark.'}
& $python.Source (Join-Path $root 'tools/universal_qa_benchmark.py') --output (Join-Path $root $OutputDirectory)
if($LASTEXITCODE-ne0){throw "Universal QA toolchain benchmark failed with exit code $LASTEXITCODE"}
