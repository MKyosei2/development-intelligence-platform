#!/usr/bin/env python3
"""Measure portfolio performance against a live DIP stack.

The script deliberately writes evidence instead of editing documentation with guessed
numbers. It is intended for CI or a controlled local machine after `docker compose up`.
"""
from __future__ import annotations

import argparse
import concurrent.futures
import datetime as dt
import json
import math
import os
import platform
import statistics
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path


def percentile(values: list[float], q: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = max(0, min(len(ordered) - 1, math.ceil(q * len(ordered)) - 1))
    return ordered[index]


def request(url: str, method: str = "GET", payload: dict | None = None, timeout: float = 20.0) -> tuple[int, bytes, float]:
    data = None if payload is None else json.dumps(payload).encode("utf-8")
    headers = {"Accept": "application/json"}
    if data is not None:
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            body = response.read()
            status = response.status
    except urllib.error.HTTPError as exc:
        body = exc.read()
        status = exc.code
    return status, body, (time.perf_counter() - started) * 1000.0


def wait_for_api(base_url: str, timeout_seconds: int = 180) -> None:
    deadline = time.monotonic() + timeout_seconds
    last_error: Exception | None = None
    while time.monotonic() < deadline:
        try:
            status, _, _ = request(f"{base_url}/health", timeout=5)
            if status == 200:
                return
        except Exception as exc:  # CI startup diagnostic
            last_error = exc
        time.sleep(2)
    raise RuntimeError(f"DIP API did not become healthy at {base_url}: {last_error}")


def create_project(base_url: str) -> str:
    payload = {
        "name": f"portfolio-benchmark-{uuid.uuid4().hex[:8]}",
        "repositoryUrl": "https://example.invalid/dip/portfolio-benchmark",
        "defaultBranch": "main",
        "releaseDateUtc": None,
    }
    status, body, _ = request(f"{base_url}/api/projects", "POST", payload)
    if status not in (200, 201):
        raise RuntimeError(f"project creation failed: HTTP {status}: {body.decode(errors='replace')}")
    return str(json.loads(body)["id"])


def measure_project_list(base_url: str, samples: int) -> dict:
    for _ in range(10):
        status, _, _ = request(f"{base_url}/api/projects")
        if status != 200:
            raise RuntimeError(f"project-list warmup returned HTTP {status}")
    durations: list[float] = []
    for _ in range(samples):
        status, _, elapsed = request(f"{base_url}/api/projects")
        if status != 200:
            raise RuntimeError(f"project-list benchmark returned HTTP {status}")
        durations.append(elapsed)
    return latency_summary(durations)


def collector_payload(project_id: str, index: int) -> dict:
    now = dt.datetime.now(dt.timezone.utc)
    started = now - dt.timedelta(milliseconds=20)
    return {
        "schemaVersion": "1.0",
        "reportId": f"benchmark-report-{index}-{uuid.uuid4().hex}",
        "externalRunId": f"benchmark-run-{index}-{uuid.uuid4().hex[:12]}",
        "attemptNumber": 1,
        "collectorType": "portfolio-benchmark",
        "collectorVersion": "1.0.0",
        "projectId": project_id,
        "repository": "https://example.invalid/dip/portfolio-benchmark",
        "commitSha": "a" * 40,
        "branch": "benchmark",
        "startedAt": started.isoformat().replace("+00:00", "Z"),
        "finishedAt": now.isoformat().replace("+00:00", "Z"),
        "status": 0,
        "metrics": [{"name": "frame_time_ms", "value": 16.0 + (index % 5) / 10.0, "unit": "ms", "baselineValue": 16.0}],
        "issues": [],
    }


def measure_collector(base_url: str, project_id: str, samples: int) -> dict:
    durations: list[float] = []
    for index in range(samples):
        status, body, elapsed = request(
            f"{base_url}/api/v1/collector-reports", "POST", collector_payload(project_id, index), timeout=30
        )
        if status not in (200, 201):
            raise RuntimeError(f"collector benchmark returned HTTP {status}: {body.decode(errors='replace')}")
        durations.append(elapsed)
    return latency_summary(durations)


def measure_100_rps(base_url: str, seconds: int) -> dict:
    target_rps = 100
    total = target_rps * seconds
    durations = [0.0] * total
    statuses = [0] * total
    start = time.perf_counter()
    lock = threading.Lock()

    def one(index: int, scheduled: float) -> None:
        delay = scheduled - time.perf_counter()
        if delay > 0:
            time.sleep(delay)
        try:
            status, _, elapsed = request(f"{base_url}/api/projects", timeout=10)
        except Exception:
            status, elapsed = 0, 0.0
        with lock:
            statuses[index] = status
            durations[index] = elapsed

    with concurrent.futures.ThreadPoolExecutor(max_workers=64) as pool:
        futures = []
        for index in range(total):
            scheduled = start + index / target_rps
            futures.append(pool.submit(one, index, scheduled))
        for future in futures:
            future.result()

    failures = sum(1 for status in statuses if status != 200)
    elapsed = time.perf_counter() - start
    summary = latency_summary([x for x, status in zip(durations, statuses) if status == 200])
    summary.update(
        {
            "targetRps": target_rps,
            "requests": total,
            "wallSeconds": round(elapsed, 3),
            "achievedRps": round(total / elapsed, 2),
            "failures": failures,
            "errorRatePercent": round(100.0 * failures / total, 4),
        }
    )
    return summary


def docker_psql(sql: str) -> str:
    command = [
        "docker", "compose", "exec", "-T", "postgres", "psql",
        "-v", "ON_ERROR_STOP=1", "-U", os.environ.get("POSTGRES_USER", "dip"),
        "-d", os.environ.get("POSTGRES_DB", "dip"), "-Atc", sql,
    ]
    completed = subprocess.run(command, check=True, text=True, capture_output=True)
    return completed.stdout.strip()


def seed_million_metrics() -> str:
    project = "00000000-0000-0000-0000-0000000000b1"
    pipeline = "00000000-0000-0000-0000-0000000000b2"
    sql = f'''\
INSERT INTO "Projects" ("Id","Name","RepositoryUrl","DefaultBranch","ReleaseDateUtc","HealthScore","CreatedAtUtc","UpdatedAtUtc")
VALUES ('{project}','Benchmark 1M','https://example.invalid/benchmark','main',NULL,100,now(),now())
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "PipelineRuns" ("Id","ProjectId","CommitSha","ExternalRunId","AttemptNumber","Branch","CollectorType","CollectorVersion","Status","QueuedAtUtc","StartedAtUtc","FinishedAtUtc","FailureReason")
VALUES ('{pipeline}','{project}',repeat('b',40),'benchmark-1m',1,'benchmark','portfolio-benchmark','1.0',2,now(),now(),now(),NULL)
ON CONFLICT ("Id") DO NOTHING;
DELETE FROM "Metrics" WHERE "PipelineRunId"='{pipeline}';
INSERT INTO "Metrics" ("Id","PipelineRunId","BaselinePipelineRunId","Name","Value","Unit","BaselineValue","Difference","DifferencePercent","Severity","ComparisonStatus","EnvironmentId","ScenarioId","Platform","BuildConfiguration","BenchmarkVersion")
SELECT md5('dip-metric-' || g::text)::uuid,'{pipeline}',NULL,'metric_' || (g % 64),g % 10000,'ms',NULL,NULL,NULL,0,0,'ci','million','linux','Release','1m-v1'
FROM generate_series(1,1000000) AS g;
ANALYZE "Metrics";
SELECT count(*) FROM "Metrics" WHERE "PipelineRunId"='{pipeline}';'''
    output = docker_psql(sql)
    if output.splitlines()[-1].strip() != "1000000":
        raise RuntimeError(f"1M metric seed did not produce 1,000,000 rows: {output}")
    return pipeline


def measure_million_metric_query(pipeline_id: str, samples: int = 10) -> dict:
    query = (
        "EXPLAIN (ANALYZE, FORMAT JSON) "
        'SELECT "Name", AVG("Value"), MAX("Value") FROM "Metrics" '
        f'WHERE "PipelineRunId"=\'{pipeline_id}\' GROUP BY "Name";'
    )
    durations: list[float] = []
    for _ in range(samples + 2):
        raw = docker_psql(query)
        plan = json.loads(raw)
        elapsed = float(plan[0]["Execution Time"])
        if len(durations) < samples and _ >= 2:
            durations.append(elapsed)
    result = latency_summary(durations)
    result["rowsInDataset"] = 1_000_000
    result["query"] = "group AVG/MAX by metric name for one pipeline"
    return result


def latency_summary(values: list[float]) -> dict:
    if not values:
        return {"samples": 0, "p50Ms": 0, "p95Ms": 0, "p99Ms": 0, "meanMs": 0}
    return {
        "samples": len(values),
        "p50Ms": round(percentile(values, 0.50), 3),
        "p95Ms": round(percentile(values, 0.95), 3),
        "p99Ms": round(percentile(values, 0.99), 3),
        "meanMs": round(statistics.fmean(values), 3),
    }


def render_markdown(result: dict) -> str:
    p = result["measurements"]
    lines = [
        "# Benchmark Results",
        "",
        "These values are generated from a live DIP stack. They are not estimates.",
        "",
        "| Scenario | Target | Actual |",
        "|---|---:|---:|",
        f"| Project list p95 | <500ms | **{p['projectList']['p95Ms']:.3f} ms** |",
        f"| Collector p95 | <1000ms | **{p['collector']['p95Ms']:.3f} ms** |",
        f"| 100req/s error rate | <1% | **{p['hundredRps']['errorRatePercent']:.4f}%** |",
        f"| 1M Metric query p95 | <2s | **{p['millionMetricQuery']['p95Ms']:.3f} ms** |",
        "",
        "## Provenance",
        "",
        f"- Commit: `{result['provenance']['commit']}`",
        f"- Runner: `{result['provenance']['runner']}`",
        f"- OS: `{result['provenance']['os']}`",
        f"- Python: `{result['provenance']['python']}`",
        f"- Generated UTC: `{result['generatedAtUtc']}`",
        "- Dataset: 1,000,000 persisted `Metrics` rows for the database-query scenario.",
        "- Raw evidence: `artifacts/portfolio-benchmark/benchmark-results.json`.",
        "",
    ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=os.environ.get("DIP_API_URL", "http://127.0.0.1:5080"))
    parser.add_argument("--output", default="artifacts/portfolio-benchmark")
    parser.add_argument("--project-samples", type=int, default=150)
    parser.add_argument("--collector-samples", type=int, default=40)
    parser.add_argument("--rps-seconds", type=int, default=15)
    args = parser.parse_args()

    base_url = args.base_url.rstrip("/")
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    wait_for_api(base_url)
    project_id = create_project(base_url)

    measurements = {
        "projectList": measure_project_list(base_url, args.project_samples),
        "collector": measure_collector(base_url, project_id, args.collector_samples),
        "hundredRps": measure_100_rps(base_url, args.rps_seconds),
    }
    pipeline_id = seed_million_metrics()
    measurements["millionMetricQuery"] = measure_million_metric_query(pipeline_id)

    result = {
        "schemaVersion": "1.0",
        "generatedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "targets": {
            "projectListP95Ms": 500,
            "collectorP95Ms": 1000,
            "hundredRpsErrorRatePercent": 1.0,
            "millionMetricQueryP95Ms": 2000,
        },
        "provenance": {
            "commit": os.environ.get("GITHUB_SHA") or subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip(),
            "runner": os.environ.get("RUNNER_NAME", "local"),
            "os": platform.platform(),
            "python": platform.python_version(),
            "machine": platform.machine(),
        },
        "measurements": measurements,
    }
    (out / "benchmark-results.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    markdown = render_markdown(result)
    (out / "benchmark-results.md").write_text(markdown, encoding="utf-8")
    print(markdown)

    failures = []
    if measurements["projectList"]["p95Ms"] >= 500: failures.append("projectList")
    if measurements["collector"]["p95Ms"] >= 1000: failures.append("collector")
    if measurements["hundredRps"]["errorRatePercent"] >= 1.0: failures.append("hundredRps")
    if measurements["millionMetricQuery"]["p95Ms"] >= 2000: failures.append("millionMetricQuery")
    if failures:
        print("Benchmark target failures: " + ", ".join(failures), file=sys.stderr)
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
