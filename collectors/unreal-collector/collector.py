#!/usr/bin/env python3
# Purpose: Implements collector responsibilities for unreal collector.
from __future__ import annotations

import argparse
import csv
import datetime as dt
import json
import re
import subprocess
import sys
import uuid
from pathlib import Path

VERSION = "1.3.0"
ASSET_EXTS = {".uasset", ".umap", ".ubulk", ".uexp"}
SOURCE_EXTS = {".cpp", ".h", ".hpp", ".cs"}
ERROR_RE = re.compile(r"(?:error:|fatal error|ensure condition failed|assertion failed)", re.I)
WARNING_RE = re.compile(r"(?:warning:|log[^:]*: warning)", re.I)
BLUEPRINT_RE = re.compile(r"(?:LogBlueprint|BlueprintLog).*(?:Error|Compile failed)", re.I)
PACKAGE_RE = re.compile(r"(?:PackagingResults|AutomationTool).*(?:Error|BUILD FAILED|Cook failed)", re.I)
SHADER_RE = re.compile(r"(?:ShaderCompileWorker|LogShaderCompilers).*(?:Error|Failed)", re.I)
NETWORK_RE = re.compile(r"(?:NetDriver|LogNet).*(?:timeout|connection lost|failure)", re.I)


def utcnow():
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def metric(name, value, unit, **extra):
    return {
        "name": name,
        "value": value,
        "unit": unit,
        **{key: value for key, value in extra.items() if value is not None},
    }


def issue(category, severity, title, description, source, rule_id=None, evidence=None):
    result = {
        "category": category,
        "severity": severity,
        "title": title[:200],
        "description": description[:4000],
        "source": source,
    }
    if rule_id:
        result["ruleId"] = rule_id
    if evidence is not None:
        result["evidenceJson"] = json.dumps(evidence, separators=(",", ":"))
    return result


def quality_gate_failed(issues: list[dict], mode: str) -> bool:
    """Return a deterministic CI gate decision for collected Unreal findings.

    ``off`` preserves collect-only behavior except structural collector failure
    (missing .uproject), ``critical`` rejects any Critical finding, and ``warning``
    rejects both Warning and Critical findings. Unknown modes fail closed.
    """
    normalized = (mode or "critical").strip().lower()
    if normalized not in {"off", "critical", "warning"}:
        raise ValueError(
            f"Unknown quality gate mode '{mode}'. Expected off, critical, or warning."
        )

    structural_failure = any(
        item.get("ruleId") == "UPROJECT_MISSING" for item in issues
    )
    if structural_failure:
        return True
    if normalized == "off":
        return False

    severities = {str(item.get("severity", "")).lower() for item in issues}
    if "critical" in severities:
        return True
    return normalized == "warning" and "warning" in severities


def _collect_automation(report: Path, metrics: list, issues: list):
    try:
        data = json.loads(report.read_text(encoding="utf-8-sig"))
        stack = [data]
        passed = failed = skipped = 0
        while stack:
            node = stack.pop()
            if isinstance(node, dict):
                state = str(
                    node.get(
                        "state",
                        node.get(
                            "State",
                            node.get("result", node.get("Result", "")),
                        ),
                    )
                ).lower()
                if state in {"success", "succeeded", "pass", "passed"}:
                    passed += 1
                elif state in {"fail", "failed", "error"}:
                    failed += 1
                elif state in {"notrun", "not run", "skipped", "skip"}:
                    skipped += 1
                stack.extend(node.values())
            elif isinstance(node, list):
                stack.extend(node)

        metrics += [
            metric("unreal.automation.passed", passed, "count"),
            metric("unreal.automation.failed", failed, "count"),
            metric("unreal.automation.skipped", skipped, "count"),
        ]
        if failed:
            issues.append(
                issue(
                    "AutomationTest",
                    "Critical",
                    "Unreal Automation Test failure",
                    f"{failed} failed Automation Test result(s) were found.",
                    str(report),
                    "UNREAL_AUTOMATION_FAILED",
                    {"failed": failed, "passed": passed, "skipped": skipped},
                )
            )
    except Exception as exc:
        issues.append(
            issue(
                "AutomationTest",
                "Warning",
                "Unable to parse Unreal Automation report",
                str(exc),
                str(report),
                "UNREAL_AUTOMATION_PARSE",
            )
        )


def build_engine_automation_command(
    unreal_editor_cmd: Path,
    uproject: Path,
    automation_filter: str,
    report_output: Path,
) -> list[str]:
    """Build a deterministic, unattended Unreal Automation invocation."""
    return [
        str(unreal_editor_cmd),
        str(uproject),
        "-unattended",
        "-nop4",
        "-nosplash",
        "-nullrhi",
        f"-ReportOutputPath={report_output}",
        f"-ExecCmds=Automation RunTests {automation_filter}; Quit",
        "-TestExit=Automation Test Queue Empty",
    ]


def run_engine_automation(
    root: Path,
    unreal_editor_cmd: Path,
    automation_filter: str,
    timeout_seconds: int,
) -> tuple[Path | None, dict, dict | None]:
    uproject = next(iter(root.glob("*.uproject")), None)
    if uproject is None:
        return None, {"attempted": False, "reason": "uproject-missing"}, None
    if not unreal_editor_cmd.is_file():
        return None, {"attempted": False, "reason": "editor-command-missing"}, issue(
            "AutomationTest",
            "Critical",
            "UnrealEditor-Cmd executable is unavailable",
            f"Configured engine command does not exist: {unreal_editor_cmd}",
            str(unreal_editor_cmd),
            "UNREAL_EDITOR_CMD_MISSING",
        )

    report_output = root / "Saved" / "Automation" / "Reports" / "DIP"
    report_output.mkdir(parents=True, exist_ok=True)
    command = build_engine_automation_command(
        unreal_editor_cmd, uproject, automation_filter, report_output
    )
    try:
        completed = subprocess.run(
            command,
            cwd=root,
            capture_output=True,
            text=True,
            timeout=max(30, min(timeout_seconds, 7200)),
            check=False,
        )
    except subprocess.TimeoutExpired as exc:
        return None, {
            "attempted": True,
            "timedOut": True,
            "command": command,
        }, issue(
            "AutomationTest",
            "Critical",
            "Unreal Automation execution timed out",
            str(exc),
            str(uproject),
            "UNREAL_AUTOMATION_TIMEOUT",
        )

    report = next(iter(report_output.glob("**/index.json")), None)
    execution = {
        "attempted": True,
        "exitCode": completed.returncode,
        "timedOut": False,
        "report": str(report) if report else None,
        "stdoutTail": completed.stdout[-4000:],
        "stderrTail": completed.stderr[-4000:],
    }
    if completed.returncode != 0:
        return report, execution, issue(
            "AutomationTest",
            "Critical",
            "Unreal Automation command failed",
            f"UnrealEditor-Cmd exited with code {completed.returncode}.",
            str(uproject),
            "UNREAL_AUTOMATION_COMMAND_FAILED",
            {"exitCode": completed.returncode},
        )
    if report is None:
        return None, execution, issue(
            "AutomationTest",
            "Critical",
            "Unreal Automation report was not produced",
            "The engine command completed but no index.json report was found.",
            str(report_output),
            "UNREAL_AUTOMATION_REPORT_MISSING",
        )
    return report, execution, None


def collect(root: Path, insights: Path | None, automation: Path | None = None):
    files = [path for path in root.rglob("*") if path.is_file()]
    uproject = next(iter(root.glob("*.uproject")), None)
    metrics = []
    issues = []

    assets = [path for path in files if path.suffix.lower() in ASSET_EXTS]
    sources = [path for path in files if path.suffix.lower() in SOURCE_EXTS]
    maps = [path for path in files if path.suffix.lower() == ".umap"]
    configs = [
        path
        for path in files
        if path.suffix.lower() == ".ini" and "Config" in path.parts
    ]
    plugins = [path for path in files if path.suffix.lower() == ".uplugin"]
    total_asset_bytes = sum(path.stat().st_size for path in assets)
    metrics += [
        metric("unreal.asset_count", len(assets), "count"),
        metric("unreal.asset_bytes", total_asset_bytes, "bytes"),
        metric("unreal.map_count", len(maps), "count"),
        metric("unreal.source_file_count", len(sources), "count"),
        metric("unreal.config_file_count", len(configs), "count"),
        metric("unreal.plugin_descriptor_count", len(plugins), "count"),
    ]

    rhi_configs = 0
    for config in configs:
        try:
            text = config.read_text(encoding="utf-8", errors="replace")
            if "DefaultGraphicsRHI" in text or "TargetedRHIs" in text:
                rhi_configs += 1
        except OSError:
            pass
    metrics.append(metric("unreal.default_rhi_config_count", rhi_configs, "count"))
    if rhi_configs == 0:
        issues.append(
            issue(
                "PlatformConfiguration",
                "Warning",
                "No explicit target RHI configuration observed",
                "Review DefaultGraphicsRHI/TargetedRHIs for the release platforms; this is a review signal, not proof of a misconfiguration.",
                str(root),
                "UNREAL_RHI_REVIEW",
            )
        )

    if uproject:
        try:
            data = json.loads(uproject.read_text(encoding="utf-8-sig"))
            metrics.append(metric("unreal.module_count", len(data.get("Modules", [])), "count"))
            metrics.append(
                metric(
                    "unreal.enabled_plugin_count",
                    sum(1 for plugin in data.get("Plugins", []) if plugin.get("Enabled", False)),
                    "count",
                )
            )
            engine = data.get("EngineAssociation")
            if engine:
                metrics.append(
                    metric(
                        "unreal.engine_association",
                        1,
                        "state",
                        benchmarkVersion=str(engine),
                    )
                )
        except Exception as exc:
            issues.append(
                issue(
                    "ProjectConfiguration",
                    "Warning",
                    "Unable to parse .uproject",
                    str(exc),
                    str(uproject),
                    "UPROJECT_PARSE",
                )
            )
    else:
        issues.append(
            issue(
                "ProjectConfiguration",
                "Critical",
                "No .uproject found",
                "The collector root does not contain an Unreal .uproject descriptor.",
                str(root),
                "UPROJECT_MISSING",
            )
        )

    log_files = sorted(
        (
            path
            for path in files
            if path.suffix.lower() == ".log"
            and ("Saved" in path.parts or "Logs" in path.parts)
        ),
        key=lambda path: path.stat().st_mtime,
        reverse=True,
    )[:20]
    errors = warnings = 0
    for log in log_files:
        try:
            for line_number, line in enumerate(
                log.read_text(encoding="utf-8", errors="replace").splitlines(), 1
            ):
                if ERROR_RE.search(line):
                    errors += 1
                    category, title, rule = (
                        "UnrealLog",
                        "Unreal error",
                        "UNREAL_LOG_ERROR",
                    )
                    if BLUEPRINT_RE.search(line):
                        category, title, rule = (
                            "Blueprint",
                            "Blueprint compile failure",
                            "UNREAL_BLUEPRINT_COMPILE",
                        )
                    elif PACKAGE_RE.search(line):
                        category, title, rule = (
                            "Packaging",
                            "Unreal packaging/cook failure",
                            "UNREAL_PACKAGING_FAILED",
                        )
                    elif SHADER_RE.search(line):
                        category, title, rule = (
                            "Shader",
                            "Unreal shader compile failure",
                            "UNREAL_SHADER_COMPILE",
                        )
                    elif NETWORK_RE.search(line):
                        category, title, rule = (
                            "Network",
                            "Unreal network/runtime failure",
                            "UNREAL_NETWORK_FAILURE",
                        )
                    if len(issues) < 200:
                        issues.append(
                            issue(
                                category,
                                "Critical",
                                title,
                                line.strip(),
                                str(log),
                                rule,
                                {"line": line_number},
                            )
                        )
                elif WARNING_RE.search(line):
                    warnings += 1
                    if len(issues) < 200:
                        issues.append(
                            issue(
                                "UnrealLog",
                                "Warning",
                                "Unreal warning",
                                line.strip(),
                                str(log),
                                "UNREAL_LOG_WARNING",
                                {"line": line_number},
                            )
                        )
        except OSError as exc:
            issues.append(
                issue(
                    "Collector",
                    "Warning",
                    "Unable to read Unreal log",
                    str(exc),
                    str(log),
                    "UNREAL_LOG_READ",
                )
            )
    metrics += [
        metric("unreal.log_error_count", errors, "count"),
        metric("unreal.log_warning_count", warnings, "count"),
    ]

    crash_files = [
        path
        for path in files
        if "Crashes" in path.parts
        and path.suffix.lower() in {".dmp", ".xml", ".runtime-xml", ".ue4crashcontext"}
    ]
    metrics.append(metric("unreal.crash_artifact_count", len(crash_files), "count"))
    if crash_files:
        issues.append(
            issue(
                "CrashArtifact",
                "Warning",
                "Unreal crash artifacts detected",
                f"{len(crash_files)} crash artifact(s) exist under Saved/Crashes. Correlate them with the current build before treating them as an active regression.",
                str(crash_files[0]),
                "UNREAL_CRASH_ARTIFACTS",
                {
                    "count": len(crash_files),
                    "examples": [str(path) for path in crash_files[:10]],
                },
            )
        )

    automation_report = (
        automation
        if automation and automation.is_file()
        else next(iter(root.glob("Saved/Automation/Reports/**/index.json")), None)
    )
    if automation_report:
        _collect_automation(automation_report, metrics, issues)

    if insights and insights.is_file():
        try:
            with insights.open(newline="", encoding="utf-8-sig") as handle:
                rows = list(csv.DictReader(handle))
            numeric = {}
            for row in rows:
                for key, value in row.items():
                    if not key or value is None:
                        continue
                    try:
                        numeric.setdefault(key, []).append(float(value))
                    except ValueError:
                        pass
            for key, values in numeric.items():
                if not values:
                    continue
                lower = key.lower()
                average = sum(values) / len(values)
                if "frametime" in lower or "frame_time" in lower:
                    metrics.append(metric("unreal.frame_time_ms.avg", average, "ms"))
                elif "gamethread" in lower:
                    metrics.append(metric("unreal.game_thread_ms.avg", average, "ms"))
                elif "renderthread" in lower:
                    metrics.append(metric("unreal.render_thread_ms.avg", average, "ms"))
                elif lower in {"gpu", "gpums", "gpu_ms"}:
                    metrics.append(metric("unreal.gpu_ms.avg", average, "ms"))
                elif "memory" in lower:
                    metrics.append(metric("unreal.memory.avg", average, "bytes"))
        except Exception as exc:
            issues.append(
                issue(
                    "Insights",
                    "Warning",
                    "Unable to parse Unreal Insights CSV",
                    str(exc),
                    str(insights),
                    "UNREAL_INSIGHTS_PARSE",
                )
            )

    return metrics, issues


def main(argv=None):
    parser = argparse.ArgumentParser(description="DIP Unreal Engine collector")
    parser.add_argument("--root", required=True)
    parser.add_argument("--project-id", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--commit-sha", required=True)
    parser.add_argument("--branch", default="main")
    parser.add_argument("--external-run-id", required=True)
    parser.add_argument("--attempt-number", type=int, default=1)
    parser.add_argument("--insights-csv")
    parser.add_argument("--automation-report")
    parser.add_argument(
        "--unreal-editor-cmd",
        help="Optional UnrealEditor-Cmd(.exe) path. When set, run Automation Tests before collection.",
    )
    parser.add_argument(
        "--automation-filter",
        default="Project",
        help="Automation test filter used with --unreal-editor-cmd (default: Project).",
    )
    parser.add_argument("--engine-timeout-seconds", type=int, default=1800)
    parser.add_argument(
        "--quality-gate",
        choices=("off", "critical", "warning"),
        default="critical",
        help="CI gate: off=collect only, critical=fail on Critical, warning=fail on Warning/Critical.",
    )
    parser.add_argument("--output", required=True)
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    started = utcnow()
    engine_execution = {"attempted": False}
    engine_issue = None
    automation_report = Path(args.automation_report).resolve() if args.automation_report else None

    if args.unreal_editor_cmd:
        automation_report, engine_execution, engine_issue = run_engine_automation(
            root,
            Path(args.unreal_editor_cmd).resolve(),
            args.automation_filter,
            args.engine_timeout_seconds,
        )

    metrics, issues = collect(
        root,
        Path(args.insights_csv).resolve() if args.insights_csv else None,
        automation_report,
    )
    metrics.append(metric("unreal.engine_native_automation_attempted", 1 if engine_execution.get("attempted") else 0, "bool"))
    if engine_issue:
        issues.append(engine_issue)
    metrics.extend(
        [
            metric(
                "unreal.issue_critical_count",
                sum(1 for item in issues if str(item.get("severity", "")).lower() == "critical"),
                "count",
            ),
            metric(
                "unreal.issue_warning_count",
                sum(1 for item in issues if str(item.get("severity", "")).lower() == "warning"),
                "count",
            ),
        ]
    )
    finished = utcnow()
    status = "Failed" if quality_gate_failed(issues, args.quality_gate) else "Succeeded"

    report = {
        "schemaVersion": "1.0",
        "reportId": str(uuid.uuid4()),
        "externalRunId": args.external_run_id,
        "attemptNumber": args.attempt_number,
        "collectorType": "unreal",
        "collectorVersion": VERSION,
        "projectId": args.project_id,
        "repository": args.repository,
        "commitSha": args.commit_sha,
        "branch": args.branch,
        "startedAt": started,
        "finishedAt": finished,
        "status": status,
        "engineNativeAutomation": engine_execution,
        "metrics": metrics,
        "issues": issues,
    }
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    return 0 if status == "Succeeded" else 2


if __name__ == "__main__":
    sys.exit(main())
