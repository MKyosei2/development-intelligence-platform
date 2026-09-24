# Purpose: Implements main responsibilities for app.
from collections import Counter
from contextlib import asynccontextmanager
import logging
from time import perf_counter
from typing import Final

from fastapi import FastAPI, Request
from fastapi.responses import PlainTextResponse

from app.api.routes import anomaly, health, release_prediction, risk, summary
from app.core.config import get_settings
from app.core.logging import configure_logging

_METRICS_CONTENT_TYPE: Final = "text/plain; version=0.0.4; charset=utf-8"
_request_counts: Counter[tuple[str, int]] = Counter()
_request_duration_seconds_sum = 0.0
_request_duration_seconds_count = 0


@asynccontextmanager
async def lifespan(_: FastAPI):
    settings = get_settings()
    configure_logging(settings.log_level)
    logging.getLogger(__name__).info(
        "Starting %s in %s mode.",
        settings.app_name,
        settings.app_env,
    )
    yield
    logging.getLogger(__name__).info("Stopping analysis service.")


settings = get_settings()
app = FastAPI(title=settings.app_name, version="0.1.0", lifespan=lifespan)


@app.middleware("http")
async def collect_http_metrics(request: Request, call_next):
    global _request_duration_seconds_count
    global _request_duration_seconds_sum

    if request.url.path == "/metrics":
        return await call_next(request)

    started = perf_counter()
    response = await call_next(request)
    elapsed = perf_counter() - started

    _request_counts[(request.method.upper(), response.status_code)] += 1
    _request_duration_seconds_sum += elapsed
    _request_duration_seconds_count += 1
    return response


@app.get("/metrics", include_in_schema=False, response_class=PlainTextResponse)
async def metrics() -> PlainTextResponse:
    lines = [
        "# HELP dip_analysis_requests_total Total HTTP requests processed by the analysis service.",
        "# TYPE dip_analysis_requests_total counter",
    ]

    for (method, status), count in sorted(_request_counts.items()):
        lines.append(
            f'dip_analysis_requests_total{{method="{method}",status="{status}"}} {count}'
        )

    lines.extend(
        [
            "# HELP dip_analysis_request_duration_seconds_sum Total request processing time.",
            "# TYPE dip_analysis_request_duration_seconds_sum counter",
            f"dip_analysis_request_duration_seconds_sum {_request_duration_seconds_sum:.9f}",
            "# HELP dip_analysis_request_duration_seconds_count Total measured requests.",
            "# TYPE dip_analysis_request_duration_seconds_count counter",
            f"dip_analysis_request_duration_seconds_count {_request_duration_seconds_count}",
            "dip_analysis_build_info{version=\"0.1.0\"} 1",
            "",
        ]
    )
    return PlainTextResponse("\n".join(lines), media_type=_METRICS_CONTENT_TYPE)


app.include_router(health.router)
app.include_router(anomaly.router)
app.include_router(risk.router)
app.include_router(summary.router)
app.include_router(release_prediction.router)
