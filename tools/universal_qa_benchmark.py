#!/usr/bin/env python3
# Purpose: Benchmarks native toolchain defect detection and reproduction with deterministic cleanup.
from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import shutil
import subprocess
import tempfile
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from statistics import mean


@dataclass
class Case:
    language: str
    tool: str
    clean_name: str
    clean_src: str
    bad_name: str
    bad_src: str
    cmd: callable
    extras: dict | None = None


def which(name):
    return shutil.which(name)


def decode_output(data):
    if data is None:
        return ""

    if isinstance(data, str):
        return data

    for encoding in ("utf-8", "cp932"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            pass

    return data.decode("utf-8", errors="replace")


def run(cmd, cwd, timeout=30):
    started = time.perf_counter()
    env = {
        **os.environ,
        "NO_COLOR": "1",
        "DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER": "1",
    }

    try:
        completed = subprocess.run(
            cmd,
            cwd=cwd,
            capture_output=True,
            timeout=timeout,
            env=env,
            text=False,
        )
        output = (
            decode_output(completed.stdout)
            + decode_output(completed.stderr)
        )
        return (
            completed.returncode,
            (time.perf_counter() - started) * 1000,
            output[-4000:],
        )
    except subprocess.TimeoutExpired:
        return (
            124,
            (time.perf_counter() - started) * 1000,
            "timeout",
        )


def cleanup_directory(path: Path, attempts: int = 8):
    last = None
    for attempt in range(attempts):
        try:
            if path.exists():
                shutil.rmtree(path)
            return
        except (PermissionError, OSError) as exc:
            last = exc
            time.sleep(min(0.15 * (2 ** attempt), 2.0))
    raise RuntimeError(
        f"Unable to clean benchmark temporary directory {path}: {last}"
    ) from last


def stop_dotnet_build_servers():
    dotnet = which("dotnet")
    if not dotnet:
        return

    try:
        subprocess.run(
            [dotnet, "build-server", "shutdown"],
            capture_output=True,
            timeout=15,
            env={
                **os.environ,
                "DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER": "1",
            },
            text=False,
        )
    except Exception:
        pass


def powershell_parse_cmd(file_name):
    escaped = file_name.replace("'", "''")
    script = (
        "$errors=$null;$tokens=$null;"
        + "[System.Management.Automation.Language.Parser]::ParseFile("
        + f"'{escaped}',[ref]$tokens,[ref]$errors)|Out-Null;"
        + "if($errors.Count -gt 0){exit 1};exit 0"
    )
    return [
        which("pwsh"),
        "-NoProfile",
        "-NonInteractive",
        "-Command",
        script,
    ]


def cases():
    return [
        Case(
            "python",
            "python",
            "app.py",
            "x=1\nprint(x)\n",
            "app.py",
            "def broken(:\n pass\n",
            lambda f: [
                which("python3") or which("python"),
                "-m",
                "py_compile",
                f,
            ],
        ),
        Case(
            "javascript",
            "node",
            "app.js",
            "const x=1; console.log(x);\n",
            "app.js",
            "function broken( {\n",
            lambda f: [which("node"), "--check", f],
        ),
        Case(
            "typescript",
            "tsc",
            "app.ts",
            "const x: number = 1;\n",
            "app.ts",
            "const x: number = ;\n",
            lambda f: [
                which("tsc"),
                "--noEmit",
                "--pretty",
                "false",
                "--target",
                "ES2020",
                f,
            ],
        ),
        Case(
            "go",
            "go",
            "main.go",
            "package main\nfunc main(){}\n",
            "main.go",
            "package main\nfunc main( {\n",
            lambda f: [which("go"), "test", "./..."],
            {"go.mod": "module dipbench\ngo 1.22\n"},
        ),
        Case(
            "java",
            "javac",
            "Main.java",
            "class Main { public static void main(String[] a){} }\n",
            "Main.java",
            "class Main { void broken( { }\n",
            lambda f: [which("javac"), f],
        ),
        Case(
            "kotlin",
            "kotlinc",
            "Main.kt",
            "fun main(){ println(1) }\n",
            "Main.kt",
            "fun main( {\n",
            lambda f: [which("kotlinc"), f, "-d", "out.jar"],
        ),
        Case(
            "ruby",
            "ruby",
            "app.rb",
            "x=1\nputs x\n",
            "app.rb",
            "def broken(\nend\n",
            lambda f: [which("ruby"), "-c", f],
        ),
        Case(
            "php",
            "php",
            "app.php",
            "<?php $x=1; echo $x; ?>\n",
            "app.php",
            "<?php function broken( { ?>\n",
            lambda f: [which("php"), "-l", f],
        ),
        Case(
            "bash",
            "bash",
            "app.sh",
            "#!/usr/bin/env bash\necho ok\n",
            "app.sh",
            "#!/usr/bin/env bash\nif then\n",
            lambda f: [which("bash"), "-n", f],
        ),
        Case(
            "swift",
            "swiftc",
            "main.swift",
            "let x:Int=1\nprint(x)\n",
            "main.swift",
            "let x:Int =\n",
            lambda f: [which("swiftc"), "-typecheck", f],
        ),
        Case(
            "objectivec",
            "clang",
            "main.m",
            "int main(void){return 0;}\n",
            "main.m",
            "int main( { return 0; }\n",
            lambda f: [which("clang"), "-fsyntax-only", f],
        ),
        Case(
            "c",
            "clang",
            "main.c",
            "int main(void){return 0;}\n",
            "main.c",
            "int main( { return 0; }\n",
            lambda f: [
                which("clang"),
                "-fsyntax-only",
                "-Wall",
                "-Wextra",
                f,
            ],
        ),
        Case(
            "cpp",
            "clang++",
            "main.cpp",
            "int main(){return 0;}\n",
            "main.cpp",
            "int main( { return 0; }\n",
            lambda f: [
                which("clang++"),
                "-fsyntax-only",
                "-Wall",
                "-Wextra",
                f,
            ],
        ),
        Case(
            "lua",
            "luac",
            "app.lua",
            "local x=1\nprint(x)\n",
            "app.lua",
            "function broken(\n",
            lambda f: [which("luac"), "-p", f],
        ),
        Case(
            "powershell",
            "pwsh",
            "app.ps1",
            "$x = 1\nWrite-Output $x\n",
            "app.ps1",
            "function Broken( {\n",
            powershell_parse_cmd,
        ),
        Case(
            "csharp",
            "dotnet",
            "Program.cs",
            "class Program { static void Main(){} }\n",
            "Program.cs",
            "class Program { static void Main( { }\n",
            lambda f: [
                which("dotnet"),
                "build",
                "--nologo",
                "--disable-build-servers",
            ],
            {
                "bench.csproj":
                    '<Project Sdk="Microsoft.NET.Sdk">'
                    "<PropertyGroup>"
                    "<TargetFramework>net8.0</TargetFramework>"
                    "<OutputType>Exe</OutputType>"
                    "</PropertyGroup>"
                    "</Project>"
            },
        ),
        Case(
            "rust",
            "rustc",
            "main.rs",
            "fn main() {}\n",
            "main.rs",
            "fn main( {\n",
            lambda f: [which("rustc"), f],
        ),
        Case(
            "sql",
            "sqlfluff",
            "app.sql",
            "SELECT 1;\n",
            "app.sql",
            "SELEC FROM;\n",
            lambda f: [which("sqlfluff"), "lint", f],
        ),
        Case(
            "glsl",
            "glslangValidator",
            "main.vert",
            "#version 450\nvoid main(){gl_Position=vec4(0.0);}\n",
            "main.vert",
            "#version 450\nvoid main( {\n",
            lambda f: [which("glslangValidator"), f],
        ),
        Case(
            "gdscript",
            "godot",
            "script.gd",
            "extends Node\nfunc _ready():\n    pass\n",
            "script.gd",
            "extends Node\nfunc broken(\n",
            lambda f: [
                which("godot") or which("godot4"),
                "--headless",
                "--editor",
                "--quit",
            ],
            {
                "project.godot":
                    "[application]\n"
                    'config/name="DIPBench"\n'
                    "[rendering]\n"
                    'renderer/rendering_method="gl_compatibility"\n'
            },
        ),
    ]


def version(tool):
    executable = which(tool)
    if not executable:
        return None

    for args in (["--version"], ["-version"], ["-v"]):
        try:
            completed = subprocess.run(
                [executable, *args],
                capture_output=True,
                timeout=5,
                text=False,
            )
            output = (
                decode_output(completed.stdout)
                + decode_output(completed.stderr)
            )
            lines = output.strip().splitlines()
            if lines:
                return lines[0][:200]
        except Exception:
            pass

    return executable


def pct(numerator, denominator):
    return (
        round(100 * numerator / denominator, 2)
        if denominator
        else 0.0
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        default="artifacts/universal-qa-benchmark",
    )
    args = parser.parse_args()

    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    results = []

    for case in cases():
        executable = which(case.tool)

        if not executable:
            results.append(
                {
                    "language": case.language,
                    "tool": case.tool,
                    "status": "skipped",
                    "reason": "tool unavailable",
                }
            )
            continue

        row = {
            "language": case.language,
            "tool": case.tool,
            "toolVersion": version(case.tool),
            "status": "executed",
        }

        temporary_directory = Path(
            tempfile.mkdtemp(prefix="dip-uqa-")
        )

        try:
            for name, value in (case.extras or {}).items():
                (temporary_directory / name).write_text(
                    value,
                    encoding="utf-8",
                )

            clean = temporary_directory / case.clean_name
            clean.write_text(
                case.clean_src,
                encoding="utf-8",
            )

            clean_code, clean_ms, _ = run(
                case.cmd(clean.name),
                temporary_directory,
            )

            clean.unlink()

            bad = temporary_directory / case.bad_name
            bad.write_text(
                case.bad_src,
                encoding="utf-8",
            )

            defect_code, defect_ms, defect_log = run(
                case.cmd(bad.name),
                temporary_directory,
            )

            reproduction_code, reproduction_ms, _ = run(
                case.cmd(bad.name),
                temporary_directory,
            )
        finally:
            if case.language == "csharp":
                stop_dotnet_build_servers()

            cleanup_directory(
                temporary_directory
            )

        row.update(
            cleanExitCode=clean_code,
            defectExitCode=defect_code,
            cleanPassed=clean_code == 0,
            defectDetected=defect_code != 0,
            reproduced=(
                defect_code != 0
                and reproduction_code != 0
            ),
            cleanDurationMs=round(clean_ms, 2),
            defectDetectionDurationMs=round(defect_ms, 2),
            reproductionDurationMs=round(
                reproduction_ms,
                2,
            ),
            durationMs=round(
                clean_ms
                + defect_ms
                + reproduction_ms,
                2,
            ),
            diagnosticPreview=defect_log[-800:],
        )

        results.append(row)

    executed = [
        result
        for result in results
        if result["status"] == "executed"
    ]

    true_positive = sum(
        result["defectDetected"]
        for result in executed
    )

    false_negative = (
        len(executed)
        - true_positive
    )

    false_positive = sum(
        not result["cleanPassed"]
        for result in executed
    )

    true_negative = (
        len(executed)
        - false_positive
    )

    reproduced = sum(
        result["reproduced"]
        for result in executed
    )

    precision = pct(
        true_positive,
        true_positive + false_positive,
    )

    recall = pct(
        true_positive,
        true_positive + false_negative,
    )

    false_positive_rate = pct(
        false_positive,
        false_positive + true_negative,
    )

    false_negative_rate = pct(
        false_negative,
        false_negative + true_positive,
    )

    detection_times = [
        result["defectDetectionDurationMs"]
        for result in executed
    ]

    reproduction_times = [
        result["reproductionDurationMs"]
        for result in executed
        if result["defectDetected"]
    ]

    slices = [
        {
            "language": result["language"],
            "detected": result["defectDetected"],
            "cleanPassed": result["cleanPassed"],
            "reproduced": result["reproduced"],
            "detectionMs":
                result["defectDetectionDurationMs"],
            "reproductionMs":
                result["reproductionDurationMs"],
        }
        for result in executed
    ]

    payload = {
        "schemaVersion": "2.0",
        "benchmark": "DIP Universal QA Toolchain Benchmark",
        "evidenceClass":
            "measured-native-toolchain-bootstrap",
        "generatedAt":
            datetime.now(timezone.utc).isoformat(),
        "environment": {
            "platform": platform.platform(),
            "python": platform.python_version(),
        },
        "executedLanguages": [
            result["language"]
            for result in executed
        ],
        "skippedLanguages": [
            result["language"]
            for result in results
            if result["status"] == "skipped"
        ],
        "cases": len(executed),
        "confusionMatrix": {
            "truePositive": true_positive,
            "falsePositive": false_positive,
            "trueNegative": true_negative,
            "falseNegative": false_negative,
        },
        "precisionPercent": precision,
        "recallPercent": recall,
        "falsePositiveRatePercent":
            false_positive_rate,
        "falseNegativeRatePercent":
            false_negative_rate,
        "reproductionRatePercent":
            pct(reproduced, len(executed)),
        "meanDetectionTimeMs":
            round(mean(detection_times), 2)
            if detection_times
            else None,
        "meanReproductionTimeMs":
            round(mean(reproduction_times), 2)
            if reproduction_times
            else None,
        "rootCauseTop1Accuracy":
            "not-measured-by-bootstrap",
        "rootCauseTop3Accuracy":
            "not-measured-by-bootstrap",
        "formalAdapterBenchmark": False,
        "note":
            "Bootstrap measures native-toolchain defect "
            "detection/reproduction only. Root-cause ranking "
            "requires labeled DIP adapter/diagnostic cases and "
            "is intentionally not inferred here.",
        "domainSlices": slices,
        "results": results,
    }

    raw = json.dumps(
        payload,
        indent=2,
        ensure_ascii=False,
    )

    result_json = (
        output
        / "universal-qa-benchmark-results.json"
    )

    result_json.write_text(
        raw,
        encoding="utf-8",
    )

    sha = hashlib.sha256(
        raw.encode()
    ).hexdigest()

    (
        output
        / "universal-qa-benchmark-results.sha256"
    ).write_text(
        sha
        + "  universal-qa-benchmark-results.json\n",
        encoding="ascii",
    )

    markdown = (
        "# DIP Universal QA Toolchain Benchmark\n\n"
        "- Evidence: **measured native toolchain bootstrap**\n"
        f"- Executed languages: **{len(executed)}** "
        f"({', '.join(payload['executedLanguages']) or 'none'})\n"
        f"- Precision: **{precision}%**\n"
        f"- Recall: **{recall}%**\n"
        f"- False-positive rate: **{false_positive_rate}%**\n"
        f"- False-negative rate: **{false_negative_rate}%**\n"
        "- Reproduction rate: "
        f"**{payload['reproductionRatePercent']}%**\n"
        "- Mean detection time: "
        f"**{payload['meanDetectionTimeMs']} ms**\n"
        "- Mean reproduction time: "
        f"**{payload['meanReproductionTimeMs']} ms**\n"
        "- Root-cause Top-1 / Top-3: "
        "**not measured by this bootstrap**\n"
        "- Skipped (tool unavailable): "
        f"{', '.join(payload['skippedLanguages']) or 'none'}\n\n"
        "Skipped toolchains are never upgraded to PASS. "
        "Formal adapter/root-cause verification requires "
        "labeled DIP cases on the exact revision.\n"
    )

    (
        output
        / "universal-qa-benchmark-results.md"
    ).write_text(
        markdown,
        encoding="utf-8",
    )

    print(markdown)

    return (
        0
        if executed
        and false_positive == 0
        and false_negative == 0
        and reproduced == len(executed)
        else 1
    )


if __name__ == "__main__":
    raise SystemExit(main())
